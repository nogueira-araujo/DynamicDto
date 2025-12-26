using System;
using System.Data.Common;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Collections.Generic;

namespace DynamicDtoCore
{
    public static class ProviderHelper
    {
        private static readonly object locker = new object();

        public static string QuotePrefix { get; private set; }
        public static string QuoteSuffix { get; private set; }

        private static DbProviderFactory factory;
        public static DbProviderFactory Factory => factory;

        private static string connectionString;
        public static string ConnectionString => connectionString;

        static ProviderHelper()
        {
            try
            {
                locker = new object();

                // Carrega o appsettings.json
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                // Lê qual conexão usar (ex: "Default")
                string connectionName = configuration["Connection"];

                if (string.IsNullOrEmpty(connectionName))
                    throw new InvalidOperationException(
                        "Configuration key 'Connection' not found in appsettings.json");

                // Lê a connection string
                connectionString = configuration.GetConnectionString(connectionName);

                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException(
                        $"Connection string '{connectionName}' not found in ConnectionStrings section.");

                // Lê o provider name
                string providerName = configuration[$"DbProviders:{connectionName}"];

                if (string.IsNullOrEmpty(providerName))
                    throw new InvalidOperationException(
                        $"Provider name for '{connectionName}' not found in DbProviders section.");

                // Registra o provider se necessário
                RegisterProviderIfNeeded(providerName);

                // Obtém a factory
                factory = DbProviderFactories.GetFactory(providerName);

                // Configura os delimitadores (Quote Prefix/Suffix)
                var commandBuilder = factory.CreateCommandBuilder();
                QuotePrefix = commandBuilder.QuotePrefix;
                QuoteSuffix = commandBuilder.QuoteSuffix;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to initialize ProviderHelper. Check appsettings.json configuration.", ex);
            }
        }

        /// <summary>
        /// Em .NET Core, alguns providers (como Microsoft.Data.SqlClient) precisam ser registrados explicitamente.
        /// </summary>
        private static void RegisterProviderIfNeeded(string invariantName)
        {
            try
            {
                // Tenta obter - se existir, não faz nada
                DbProviderFactories.GetFactory(invariantName);
                return; // Já está registrado
            }
            catch
            {
                // Não está registrado, vamos tentar encontrar e registrar dinamicamente
            }

            try
            {
                // Tenta inferir o nome completo do tipo da factory
                // Convenções comuns:
                // 1. [InvariantName].[LastPart]Factory (ex: Npgsql.NpgsqlFactory)
                // 2. [InvariantName].Factory (ex: MySql.Data.MySqlClient.MySqlClientFactory)

                string[] possibleFactoryNames = GetPossibleFactoryTypeNames(invariantName);

                Type factoryType = null;
                Assembly factoryAssembly = null;

                // Primeiro tenta nos assemblies já carregados
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in loadedAssemblies)
                {
                    foreach (var typeName in possibleFactoryNames)
                    {
                        factoryType = assembly.GetType(typeName, false, true);
                        if (factoryType != null)
                        {
                            factoryAssembly = assembly;
                            break;
                        }
                    }
                    if (factoryType != null) break;
                }

                // Se não encontrou, tenta carregar o assembly dinamicamente
                if (factoryType == null)
                {
                    // Extrai possíveis nomes de assembly do invariant name
                    string[] possibleAssemblyNames = GetPossibleAssemblyNames(invariantName);

                    foreach (var assemblyName in possibleAssemblyNames)
                    {
                        try
                        {
                            factoryAssembly = Assembly.Load(assemblyName);

                            foreach (var typeName in possibleFactoryNames)
                            {
                                factoryType = factoryAssembly.GetType(typeName, false, true);
                                if (factoryType != null) break;
                            }

                            if (factoryType != null) break;
                        }
                        catch
                        {
                            // Tenta próximo nome de assembly
                        }
                    }
                }

                // Última tentativa: procura DLLs na pasta do executável
                if (factoryType == null)
                {
                    var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string[] possibleDllNames = GetPossibleAssemblyNames(invariantName)
                        .Select(name => Path.Combine(exePath, $"{name}.dll"))
                        .Where(File.Exists)
                        .ToArray();

                    foreach (var dllPath in possibleDllNames)
                    {
                        try
                        {
                            factoryAssembly = Assembly.LoadFrom(dllPath);

                            foreach (var typeName in possibleFactoryNames)
                            {
                                factoryType = factoryAssembly.GetType(typeName, false, true);
                                if (factoryType != null) break;
                            }

                            if (factoryType != null) break;
                        }
                        catch
                        {
                            // Continua tentando
                        }
                    }
                }

                // Se ainda não encontrou, tenta procurar em todos os tipos do assembly
                if (factoryType == null && factoryAssembly != null)
                {
                    factoryType = factoryAssembly.GetTypes()
                        .FirstOrDefault(t => typeof(DbProviderFactory).IsAssignableFrom(t)
                                             && !t.IsAbstract
                                             && t.Name.EndsWith("Factory", StringComparison.OrdinalIgnoreCase));
                }

                if (factoryType == null)
                {
                    throw new NotSupportedException(
                        $"Provider '{invariantName}' could not be found. " +
                        $"Ensure the provider assembly is installed (via NuGet) or present in the application directory.");
                }

                // Obtém a instância singleton da factory (propriedade Instance)
                var instanceProperty = factoryType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);

                if (instanceProperty == null)
                {
                    throw new NotSupportedException(
                        $"Provider factory '{factoryType.FullName}' does not have a public static 'Instance' property.");
                }

                var factoryInstance = instanceProperty.GetValue(null) as DbProviderFactory;

                if (factoryInstance == null)
                {
                    throw new NotSupportedException(
                        $"Could not obtain DbProviderFactory instance from '{factoryType.FullName}.Instance'.");
                }

                // Registra o provider
                DbProviderFactories.RegisterFactory(invariantName, factoryInstance);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to register provider '{invariantName}'. " +
                    $"Ensure the provider package is installed and accessible.",
                    ex);
            }
        }

        /// <summary>
        /// Gera possíveis nomes de tipo da factory baseado no invariant name
        /// </summary>
        private static string[] GetPossibleFactoryTypeNames(string invariantName)
        {
            var parts = invariantName.Split('.');
            var lastPart = parts[parts.Length - 1];

            return new[]
            {
        // Padrão: Npgsql.NpgsqlFactory
        $"{invariantName}.{lastPart}Factory",
        
        // Padrão alternativo: MySql.Data.MySqlClient.MySqlClientFactory
        $"{invariantName}.{lastPart.Replace(".", "")}Factory",
        
        // Padrão: System.Data.SQLite.SQLiteFactory
        $"{invariantName}.{lastPart}Factory",
        
        // Caso o invariant name já termine com o nome da classe
        $"{invariantName}Factory",
        
        // Oracle usa Client suffix: Oracle.ManagedDataAccess.Client.OracleClientFactory
        lastPart == "Client" && parts.Length > 1
            ? $"{invariantName}.{parts[parts.Length - 2]}ClientFactory"
            : null
    }.Where(s => s != null).Distinct().ToArray();
        }

        /// <summary>
        /// Gera possíveis nomes de assembly baseado no invariant name
        /// </summary>
        private static string[] GetPossibleAssemblyNames(string invariantName)
        {
            var parts = invariantName.Split('.');

            var names = new List<string>
    {
        // Nome completo: MySql.Data.MySqlClient
        invariantName,
        
        // Primeira parte: MySql
        parts[0],
        
        // Duas primeiras partes: MySql.Data
        parts.Length > 1 ? string.Join(".", parts.Take(2)) : null,
        
        // Três primeiras partes: Oracle.ManagedDataAccess.Client
        parts.Length > 2 ? string.Join(".", parts.Take(3)) : null
    };

            return names.Where(s => s != null).Distinct().ToArray();
        }

        public static DbConnection CreateConnection()
        {
            lock (locker)
            {
                DbConnection conn = factory.CreateConnection();
                conn.ConnectionString = connectionString;
                return conn;
            }
        }

        /// <summary>
        /// Cria um comando para a conexão fornecida
        /// </summary>
        public static DbCommand CreateCommand(DbConnection connection, string commandText = null)
        {
            var command = factory.CreateCommand();
            command.Connection = connection;

            if (!string.IsNullOrEmpty(commandText))
                command.CommandText = commandText;

            return command;
        }

        /// <summary>
        /// Formata um identificador (tabela/coluna) com os delimitadores corretos
        /// </summary>
        public static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return identifier;

            return $"{QuotePrefix}{identifier}{QuoteSuffix}";
        }
    }
}