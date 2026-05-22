// See https://aka.ms/new-console-template for more information
using DynamicDtoCore;
using DynamicDtoCore.Tester;
using System.Data.Common;

Console.WriteLine("Hello, World!");

//use AdventureWorks2025 database sample from Microsoft
const string SQL = @"SELECT TOP (1000) [ProductModelID]
      ,[Name]
      ,[CatalogDescription]
      ,[rowguid]
      ,[ModifiedDate]
  FROM [AdventureWorksLT2025].[SalesLT].[ProductModel]";


dynamic instance;
IProduct instance2;

DoSelectWithDynamicResult(SQL, out instance);
Console.WriteLine();
DoSelectWithDynamicResultGeneric<IProduct>(SQL, out instance2);

Console.ReadLine();

//using the DynamicClass attribute to specify the name of the dynamic class to be created
static void DoSelectWithDynamicResultGeneric<T>(string SQL, out T instance) where T : class
{
    //using a connection created externally, which will be passed to the DynamicClassFactory constructor
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
        }
    }
}

//using the DynamicClass attribute to specify the name of the dynamic class to be created
[DynamicDtoCore.DynamicClass("MinhaClasseDeTeste")]
static void DoSelectWithDynamicResult(string SQL, out dynamic instance)
{
    //using a connection created externally, which will be passed to the DynamicClassFactory constructor
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
        }
    }
}

//using the default constructor of DynamicClassFactory, which will create a connection internally
using (var factory = new DynamicClassFactory())
{
    foreach (var item in factory.Select<IProduct>(SQL))
    {
        Console.WriteLine(string.Format("Name: {0}, ModifiedDate: {1}", item.Name, item.ModifiedDate));
    }
}

Console.ReadLine();