using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MatterHackers.MatterControl.DataStorage
{
	[AttributeUsage(AttributeTargets.Property)]
	public class PrimaryKeyAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class AutoIncrementAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Property)]
	public class IndexedAttribute : Attribute
	{
		public string Name { get; set; }

		public int Order { get; set; }

		public virtual bool Unique { get; set; }

		public IndexedAttribute()
		{
		}

		public IndexedAttribute(string name, int order)
		{
			Name = name;
			Order = order;
		}
	}

	public interface ISQLite
	{
		int Insert(object obj);

		int CreateTable(Type ty);

		int DropTable(Type ty);

		int Update(object obj);

		int Delete(object obj);

		ITableQuery<T> Table<T>() where T : new();

		List<T> Query<T>(string query, params object[] args) where T : new();

		//SQLiteCommand CreateCommand(string cmdText, params object[] ps);
		T ExecuteScalar<T>(string query, params object[] args);

		int InsertAll(System.Collections.IEnumerable objects);

		void RunInTransaction(Action action);

		void Close();
	}

	public interface ITableQuery<T>
	{
		ITableQuery<T> Where(Expression<Func<T, bool>> predExpr);

		ITableQuery<U> Clone<U>();

		int Count();

		ITableQuery<T> Deferred();

		T ElementAt(int index);

		T First();

		T FirstOrDefault();

		System.Collections.Generic.IEnumerator<T> GetEnumerator();

		ITableQuery<TResult> Join<TInner, TKey, TResult>(ITableQuery<TInner> inner, System.Linq.Expressions.Expression<Func<T, TKey>> outerKeySelector, System.Linq.Expressions.Expression<Func<TInner, TKey>> innerKeySelector, System.Linq.Expressions.Expression<Func<T, TInner, TResult>> resultSelector);

		ITableQuery<T> OrderBy<U>(System.Linq.Expressions.Expression<Func<T, U>> orderExpr);

		ITableQuery<T> OrderByDescending<U>(System.Linq.Expressions.Expression<Func<T, U>> orderExpr);

		ITableQuery<TResult> Select<TResult>(System.Linq.Expressions.Expression<Func<T, TResult>> selector);

		ITableQuery<T> Skip(int n);

		ITableQuery<T> Take(int n);
	}
}