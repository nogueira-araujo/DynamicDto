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

using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
{
    var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
    var results = factory.Select(SQL);
    foreach (var item in results)
    {
        Console.WriteLine($"{item.FirstName} {item.LastName}");
    }
}

using (DbConnection connection = DynamicDtoCore.ProviderHelper.CreateConnection())
{
    var factory = new DynamicDtoCore.DynamicClassFactory(connection.CreateCommand());
    var results = factory.Select<IPerson>(SQL);
    foreach (var item in results)
    {
        Console.WriteLine($"{item.FirstName} {item.LastName}");
        Console.WriteLine(item is IPerson);
    }
}

Console.ReadLine();

