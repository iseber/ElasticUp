![ElasticUp Logo](/ElasticUp/ElasticUp/Resources/elasticup-icon2.png) 
# ElasticUp for C&#35;
Easy ElasticSearch data migrations for continuous integration and deployment! Inspired by tools like DbUp for Sql.

## Why ElasticUp
When developing new features it is possible your datamodel changes. This may require an update of your existing data in ElasticSearch. Or maybe you just want to reindex your data while doing some transformations. We wanted a tool similar to [DbUp](https://dbup.github.io/) for sql, that we could use in our [TeamCity] (https://www.jetbrains.com/teamcity) build and while deploying new software versions with [Octopus Deploy](http://github.com). 

Our requirements for ElasticUp are:
- automate the migration and transformation of data in your ElasticSearch
- track history of executed migrations so they run once and only once
- offer sensible base classes and helpers to implement your own migrations
- make it possible to create a console application that uses ElasticUp, so you can create your own migration console application and run it from TeamCity, Octopus Deploy, Visual Studio, ....


## The concept

The preferred way of using ElasticUp is by using version numbers in your index names. T

Imagine an index **my-index-v0** with alias **my-index** (your application should always talk to the alias **my-index**).

A possible scenario could be: I want to rename the field "abc" to "someUsefulName". Using ElasticUp you would create a Migration containing 3 operations:
- CreateIndexOperation: create **my-index-v1**
- BatchUpdateOperation: reindex and transform your data from **my-index-v0** to **my-index-v1** 
- SwitchAliasOperation: move the alias **my-index** from **my-index-v0** to **my-index-v1**
(- DeleteIndexOperation: you can delete the old index **my-index-v0** if you want to)


## Keeping track of the migrations

ElasticUp keeps track of which migrations already ran in a separate index. By default it will use an index 'elasticupmigrationhistory-v0' with aliasname 'elasticupmigrationhistory'. If the index does not exist yet it will create it first. You can pass your own migrationhistory alias name to ElasticUp if you want the index to have a different name.

When running ElasticUp it will check the elasticupmigrationhistory index to find out which migrations already ran. It will then run the migrations that didn't run yet in the order you provided. 

After executing a migration ElasticUp will add an 'elasticupmigrationhistory' document in your elasticupmigrationhistory index containing the classname of your migrationhistory and the datetime it was executed. This means you should NOT rename your migration classnames after they were already executed because ElasticUp tracks the migrations by classname!


## How to implement your own migrations

Create a console application and add [ElasticUp as a nuget dependency](https://www.nuget.org/packages/ElasticUp.ElasticUp.ElasticUp/).
Create an ElasticUp instance and add your migration classes to it.

```cs
public static class Program
{
	static int Main(string[] args)
	{
		var settings = new ConnectionSettings(new Uri(ConfigurationManager.AppSettings["elasticsearch_url"]))
			.DefaultIndex("my-application-index"))
			.ThrowExceptions(true); //Must be true! If set to false it's too easy to swallow exceptions!
			
		var elasticClient = new ElasticClient(settings);
		
		new ElasticUp.ElasticUp(elasticClient)
			.WithMigrationHistoryIndexAliasName("elasticup-migration-history")) //defaults to elasticupmigrationhistory unless specified
			.Migration(new Migration001_DoSomeStuff())        //your migrations here
			.Migration(new Migration002_DoSomeOtherStuff())   //the order of the classes is your responsibility. Name them wisely!
			.Run();		

		return 0;
	}
}
```

### The default case
The default use case is that your ElasticSearch has an index name with a version, and an alias without version.

```cs
public class 001_YourFirstMigration : DefaultElasticUpMigration 
{
  public 001_YourFirstMigration() : base("index-alias")
  {
      Operation(new ReindexTypeOperation<SampleObject>()); //this operation is offered by ElasticUp to just copy the documents from/to
      Operation(new MyCustomOperationToDoSomethingSpecial<OtherSampleObject>()); //implement your own operation to do anything you want
  }
} 
```

When writing a migration (by extending DefaultElasticUpMigration) the following will happen:

1. ElasticUp will find out the IndexName and version based on the given alias. ElasticUp now understands it will migrate something from e.g. myindex-v3 to myindex-v4

2. check if the migration has already run by checking the MigrationHistory documents in the source index

3. copy the MigrationHistory documents from the source index to the target index

4. do the actual migration work by running all operations in the migration (this is your actual code)

5. add this migration to the MigrationHistory in the target index

6. move the alias from the source index to the target index

Because your application talks to ElasticSearch by using the alias, your migration will have no impact on the application. It just migrates data from myindex-v3 to myindex-v4 and moves the alias to myindex-v4.


### The custom case
In some cases you may want to do something different than migrating from vX to vY. In this case your migration can extend CustomElasticUpMigration. You can override and implement almost any step of a migration, so you are free to do as you want.


```cs
public class Migration010_DoSomethingCustom : CustomElasticUpMigration
{
   public Migration001_DoCrazyStuff() : base("source-index-name", "target-index-name")
   {
       Operation(new MyFirstOperationCreateTargetIndexAndAlias());
       Operation(new ReindexTypeOperation().WithTypeName("yourobjecttype"));
       Operation(new OperationDeleteSomeStuffFromOldIndex());
   }
   
   protected override void PostMigrationTasks()
   {
      super.PostMigrationTasks(); // if you override methods make sure you call super if needed (history is tracked in super)
      DoSomethingExtraordinary();
   }
}
```

**With great power comes great responsibility**: remember, if you add custom migrations, make sure you don't forget to add the elasticupmigrationhistory.


## Do's and don'ts

- don't rename your migration classes or change the migrationnumber. The migrationhistory uses a combination of the migrationnumber and the class name to track which migrations have already run.
- Each migration works from a certain index to another index
- Each migration can contain 1 or more operations
- According to ElasticSearch, best practices is to use an index per type as much as possible. If you do this you will probably have only 1 operation for each migration. If you have multiple types in your index, you should add an operation for each type.
- You can completely override any behavior, but make sure you don't mess up the tracking of the history. Otherwise your migration will run every time ElasticUp runs.
