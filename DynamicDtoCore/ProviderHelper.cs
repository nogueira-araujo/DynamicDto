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

                // Lê qual conexão usar
                string connectionName = configuration["Connection"];
                //não deve fazer nada se não houver conexão definida e o carregamento da string de conexão se encerra aqui
                if (!string.IsNullOrEmpty(connectionName))
                {
                    // Lê a connection string
                    connectionString = configuration.GetConnectionString(connectionName);
                    // Lê o provider info (ex: "Npgsql, Npgsql.NpgsqlFactory")
                    string providerInfo = configuration[$"DbProviders:{connectionName}"];

                    if (!string.IsNullOrEmpty(providerInfo))
                    {
                        if (providerInfo.Contains(","))
                        {
                            string assemblyName;
                            string factoryTypeName;
                            var parts = providerInfo.Split(new[] { ',' }, 2);
                            assemblyName = parts[0].Trim();
                            factoryTypeName = parts[1].Trim();
                            // Registra e retorna o provider se necessário
                            factory = TryGetProviderFactory(assemblyName, factoryTypeName);

                            var commandBuilder = factory.CreateCommandBuilder();
                            QuotePrefix = commandBuilder.QuotePrefix;
                            QuoteSuffix = commandBuilder.QuoteSuffix;
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                  $"Provider info is an incorrect format.");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                               $"Provider info for '{connectionName}' not found in DbProviders section.");
                    }
                }
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
        private static DbProviderFactory TryGetProviderFactory(string assemblyName, string factoryTypeName)
        {
            try
            {
                // Tenta obter a factory - se já estiver registrada, retorna
                try
                {
                    var factory = DbProviderFactories.GetFactory(factoryTypeName);
                    if (factory != null)
                        return factory;
                }
                catch { }

                // Tenta encontrar o tipo
                Type factoryType = null;

                // 1. Procura nos assemblies já carregados
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in loadedAssemblies)
                {
                    if (assembly.GetName().Name == assemblyName ||
                        assembly.FullName.StartsWith(assemblyName + ","))
                    {
                        factoryType = assembly.GetType(factoryTypeName, false, true);
                        if (factoryType != null) break;
                    }
                }

                // 2. Se não encontrou, tenta carregar o assembly
                if (factoryType == null)
                {
                    try
                    {
                        var assembly = Assembly.Load(assemblyName);
                        factoryType = assembly.GetType(factoryTypeName, false, true);
                    }
                    catch
                    {
                        // Tenta carregar da pasta do executável
                        var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                        var dllPath = Path.Combine(exePath, $"{assemblyName}.dll");

                        if (File.Exists(dllPath))
                        {
                            var assembly = Assembly.LoadFrom(dllPath);
                            factoryType = assembly.GetType(factoryTypeName, false, true);
                        }
                    }
                }

                if (factoryType == null)
                {
                    throw new TypeLoadException(
                        $"Could not load type '{factoryTypeName}' from assembly '{assemblyName}'. " +
                        $"Ensure the provider package is installed.");
                }

                // Valida que é um DbProviderFactory
                if (!typeof(DbProviderFactory).IsAssignableFrom(factoryType))
                {
                    throw new InvalidOperationException(
                        $"Type '{factoryTypeName}' is not a DbProviderFactory.");
                }

                // Obtém a instância singleton (propriedade Instance)
                MemberInfo instanceMember = factoryType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceMember == null) {
                    instanceMember = factoryType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                }

                if (instanceMember == null)
                {
                    throw new MissingMemberException(
                        $"Type '{factoryTypeName}' does not have a public static 'Instance' property.");
                }

                DbProviderFactory factoryInstance = null;
                if (instanceMember is PropertyInfo propInfo)
                {
                    factoryInstance = propInfo.GetValue(null) as DbProviderFactory;
                }
                else if(instanceMember is FieldInfo fieldInfo)
                {
                    factoryInstance = fieldInfo.GetValue(null) as DbProviderFactory;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Member 'Instance' of type '{factoryTypeName}' is neither a property nor a field.");
                }

                if (factoryInstance == null)
                {
                    throw new InvalidOperationException(
                        $"Could not obtain DbProviderFactory instance from '{factoryTypeName}.Instance'.");
                }

                // Registra o provider usando o assembly name como invariant name
                DbProviderFactories.RegisterFactory(assemblyName, factoryInstance);
                return factoryInstance;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to register provider '{assemblyName}', '{factoryTypeName}'. " +
                    $"Check the format and ensure the provider assembly is available.",
                    ex);
            }
        }

        public static DbConnection CreateConnection()
        {
            lock (locker)
            {
                if (string.IsNullOrEmpty(connectionString))
                    throw new InvalidOperationException(
                        "Configuration key 'Connection' not found in appsettings.json or ConnectionString is not defined");
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