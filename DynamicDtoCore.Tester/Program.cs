// See https://aka.ms/new-console-template for more information
using DynamicDtoCore.Tester;
using System.Data.Common;

Console.WriteLine("Hello, World!");

//use AdventureWorks2025 database sample from Microsoft
const string SQL = @"SELECT TOP (1000) [BusinessEntityID]
      ,[PersonType]
      ,[NameStyle]
      ,[Title]
      ,[FirstName]
      ,[MiddleName]
      ,[LastName]
      ,[Suffix]
      ,[EmailPromotion]
      ,[AdditionalContactInfo]
      ,[Demographics]
      ,[rowguid]
      ,[ModifiedDate]
FROM[AdventureWorks2025].[Person].[Person]";


dynamic instance;
IPerson instance2;

DoSelectWithDynamicResult(SQL, out instance);
Console.WriteLine();
DoSelectWithDynamicResultGeneric<IPerson>(SQL, out instance2);

var method = instance.GetType().GetMethods()[17];

Console.ReadLine();

static void DoSelectWithDynamicResultGeneric<T>(string SQL, out T instance) where T : class
{
    using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
    {
        instance = null;
        var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
        var results = factory.Select<T>(SQL);
        int i = 0;
        foreach (var item in results)
        {
            if (i == 0)
            {
                Console.WriteLine(item.GetType().FullName);
                Console.WriteLine(item.ToString());
                instance = item;
                return;
                i++;
            }
            //if(item is IPerson)
            //Console.WriteLine($"{item.FirstName} {item.LastName}");
        }
    }
}

//Console.ReadLine();

[DynamicDtoCore.DynamicClass("MinhaClasseDeTeste")]
static void DoSelectWithDynamicResult(string SQL, out dynamic instance)
{
    using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
    {
        instance = null;
        var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
        var results = factory.Select(SQL);
        int i = 0;
        foreach (var item in results)
        {
            if (i == 0)
            {
                Console.WriteLine(item.GetType().FullName);
                Console.WriteLine(item.ToString());
                instance = item;
                return;
                i++;
            }
            //Console.WriteLine($"{item.FirstName} {item.LastName}");
        }
    }
}