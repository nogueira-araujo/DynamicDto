[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
# DynamicDto / DynamicDtoCore

Biblioteca para geração dinâmica de DTOs em tempo de execução a partir de resultados ADO.NET. Não é um ORM completo, mas pode ser usada como camada de materialização/projeção em um micro[...] 

## Recursos principais
- Gera tipos CLR em runtime com `System.Reflection.Emit` com propriedades baseadas nas colunas de resultado.
- Suporta retorno como `IEnumerable<dynamic>` ou `IEnumerable<TInterface>` (gera uma classe que implementa a interface).
- Cache de tipos gerados (`ConcurrentDictionary`) para reduzir custo de emissão repetida.
- Pré-processa argumentos (flatten arrays), converte enums para tipos subjacentes e trata `DBNull`.
- Suporte a geração opcional de nomes de parâmetros conforme provider (configurável).

## Requisitos
- .NET 10 (o workspace também contém projetos com compatibilidade .NET Framework)
- Referência ao provider ADO.NET desejado (ex: `Microsoft.Data.SqlClient`, `Npgsql`, etc.)

## Estrutura relevante
- `DynamicDtoCore` — núcleo: `ProviderHelper`, `DynamicClassFactory`, `ConfigurationHelper`.
- `DynamicDtoCore.Tester` — exemplo/console para testar consultas.
- `appsettings.json` — configuração de connection string e provedores.

## Configuração (exemplo `appsettings.json`)

```json
{
  "Connection": "Default",
  "ConnectionStrings": {
    "Default": "Server=.;Database=AdventureWorks2025;Trusted_Connection=True;"
  },
  "DbProviders": {
    "Default": "Microsoft.Data.SqlClient.SqlClientFactory"
  },
  "DynamicDto": {
    "UseDbParameterName": true,
    "ParameterPrefix": "@"
  }
}
```

- `Connection` — chave usada para selecionar a connection string.
- `ConnectionStrings:{name}` — connection string.
- `DbProviders:{name}` — informação do provider (pode ser `"Assembly, FullTypeName"` ou `"FullTypeName"`).
- Opções adicionais controladas por `ConfigurationHelper` (ex.: nomes de parâmetro).

## Uso rápido
Exemplo mínimo (veja `DynamicDtoCore.Tester\\Program.cs`):

```csharp
using (var conn = DynamicDtoCore.ProviderHelper.CreateConnection())
{
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT TOP 10 FirstName, LastName FROM Person.Person";
    var factory = new DynamicDtoCore.DynamicClassFactory(cmd);

    var results = factory.Select(cmd.CommandText);
    foreach (var row in results)
    {
        Console.WriteLine($"{row.FirstName} {row.LastName}");
    }
}
```

Com interface:

```csharp
public interface IPerson { string FirstName { get; } string LastName { get; } }

var results = factory.Select<IPerson>(sql);
foreach (var p in results) Console.WriteLine($"{p.FirstName} {p.LastName}");
```

## Observações importantes
- Performance: `Reflection.Emit` tem custo na primeira criação do tipo. Pré?aquecimento (pre-warm) recomendado para shapes usados com frequência.
- Nomeação: nomes de tipos são gerados com base em `StackTrace` e `DynamicClassAttribute`. Isso pode ser frágil (inlining/otimizações). Fornecer nomes explícitos seria melhor para produçã[...]
- Parâmetros: ajuste `ParameterPrefix` e `UseDbParameterName` para corresponder ao Provider (alguns providers exigem parâmetros nomeados).
- Serialização e debugging: tipos gerados em runtime podem causar dificuldades em serializadores e diagnósticos; considere geração em tempo de build se precisar de contratos estáveis.
- Segurança: sempre use parâmetros (não concatenação de SQL) para evitar injeção.

## Integração com um ORM
- Pode ser usada como camada de projeção/materialização.
- Combine com um builder de comandos/queries e gestão de transações para compor um micro?ORM.
- Para funcionalidades completas (change tracking, migrações), prefira ORMs maduros ou adicione camadas adicionais.

## Melhorias sugeridas
- API para nomes/namespaces explícitos para tipos gerados.
- Persistência ou pré?geração de types para evitar emit em runtime.
- Mapeamentos e conversões configuráveis (coluna?propriedade).
- Proteção adicional contra criação concorrente duplicada de tipos.

## Contribuição e licença
- Sob licença MIT.
- Issues e PRs são bem vindos: bugs, suporte a providers, melhorias de performance.
