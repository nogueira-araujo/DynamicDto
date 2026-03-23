// See https://aka.ms/new-console-template for more information
using DynamicDtoCore.Tester;
using Microsoft.Data.SqlClient;
using System;
using System.Data.Common;
using System.Data.SqlTypes;

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

DoSelectWithDynamicResult(SQL);

using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
{
    var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
    var results = factory.Select<IPerson>(SQL);
    int i = 0;
    foreach (var item in results)
    {
        if (i == 0)
        {
            Console.WriteLine(item.GetType().FullName);
            i++;
        }
        //if(item is IPerson)
        //Console.WriteLine($"{item.FirstName} {item.LastName}");
    }
}

Console.ReadLine();

[DynamicDtoCore.DynamicClass("MinhaClasseDeTeste")]
static void DoSelectWithDynamicResult(string SQL)
{
    using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
    {
        var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
        var results = factory.Select(SQL);
        int i = 0;
        foreach (var item in results)
        {
            if (i == 0)
            {
                Console.WriteLine(item.GetType().FullName);
                i++;
            }
            //Console.WriteLine($"{item.FirstName} {item.LastName}");
        }
    }
}