using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Castle.DynamicProxy;

namespace CastleDynamicProxy
{
	public interface IDelta<T>
	{
		T OldValue { get; set; }
		T NewValue { get; set; }
	}

	public class Delta<T> : IDelta<T>
	{
		public T OldValue { get; set; }
		public T NewValue { get; set; }
	}

	public interface IAuditable
	{
		void StartTrackingChanges();
		void StopTrackingChanges();
		bool IsBeingAudited();
		IDelta<object> GetChanges(string propertyName);
		IDelta<TProperty> GetChanges<TProperty>(MemberExpression member);
		Dictionary<string, IDelta<object>> GetChanges();
		void ExcludeProperty<TObject, TProperty>(Expression<Func<TObject, TProperty>> property);
	}

	internal class AuditableInterceptor : IInterceptor, IAuditable
	{
		private bool _isTrackingChanges;
		private readonly Dictionary<string, IDelta<Object>> _changes = new Dictionary<string, IDelta<Object>>();
		private readonly List<string> _propertiesToIgnore = new List<string>();

		public void StartTrackingChanges()
		{
			_isTrackingChanges = true;
		}

		public void StopTrackingChanges()
		{
			_isTrackingChanges = false;
		}

		public bool IsBeingAudited()
		{
			return _isTrackingChanges;
		}

		public IDelta<Object> GetChanges(string propertyName)
		{
			return _changes.ContainsKey(propertyName)				
				? _changes[propertyName] 
				: null;
		}

		public IDelta<TProperty> GetChanges<TProperty>(MemberExpression member)
		{
			if (member != null)
			{
				var property = member.Member;
				var changes = GetChanges(property.Name);
				if (changes == null) return null;

				var stronlgyTypeInstance = MakeStronglyTypeDelta<TProperty>();
				stronlgyTypeInstance.OldValue = (TProperty)changes.OldValue;
				stronlgyTypeInstance.NewValue = (TProperty)changes.NewValue;

				return stronlgyTypeInstance;
			}

			throw new ArgumentException();
		}

		public Dictionary<string, IDelta<Object>> GetChanges()
		{
			return _changes
				.Where(x => x.Value.OldValue != x.Value.NewValue)
				.ToDictionary(pair => pair.Key, pair => pair.Value);
		}

		public void ExcludeProperty<TObject, TProperty>(Expression<Func<TObject, TProperty>> property)
		{
			if(property == null)
				return;

			var member = (MemberExpression)property.Body;
			var propertyName = member.Member.Name;
			if (_changes.ContainsKey(propertyName))
				_changes.Remove(propertyName);

			_propertiesToIgnore.Add(propertyName);
		}

		public void Intercept(IInvocation invocation)
		{
			var propertyName = invocation.Method.Name.Replace("set_", string.Empty);
			var propertyIsASetter = invocation.Method.Name.StartsWith("set_", StringComparison.OrdinalIgnoreCase);
			var propertyIsNotBeingIgnored = !_propertiesToIgnore.Contains(propertyName);

			var weAreTrackingChanges = _isTrackingChanges && propertyIsASetter && propertyIsNotBeingIgnored;
			if (weAreTrackingChanges)
			{
				if (!_changes.ContainsKey(propertyName))
				{
					_changes.Add(propertyName, new Delta<object>());
				}

				var delta = _changes[propertyName];

				if (delta.OldValue == null)
				{
					var oldValue = invocation.InvocationTarget.GetType()
						.GetProperty(propertyName)
						.GetValue(invocation.InvocationTarget, null);
					delta.OldValue = oldValue;
				}
				delta.NewValue = invocation.GetArgumentValue(0);
			}

			invocation.Proceed();
		}

		private static dynamic MakeStronglyTypeDelta<T>()
		{
			var genericDeltaDefinition = typeof(Delta<>);
			var typeArgs = new[] { typeof(T) };
			var makeGenericType = genericDeltaDefinition.MakeGenericType(typeArgs);
			return Activator.CreateInstance(makeGenericType);
		}
	}

	public static class AuditableProxy
	{
		private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();
		private static readonly IDictionary<object, IAuditable> _auditedObjects = new Dictionary<object, IAuditable>();

		public static IDelta<TProperty> GetChanges<TObject, TProperty>(this TObject obj, Expression<Func<TObject, TProperty>> expression)
		{
			PerformIsAuditableCheck(obj);

			var member = expression.Body as MemberExpression;

			if (member != null)
				return _auditedObjects[obj].GetChanges<TProperty>(member);

			throw new ArgumentException();
		}

		public static TAuditable MakeAuditable<TAuditable>() where TAuditable : class, new()
		{
			var auditableInterceptor = new AuditableInterceptor();
			var proxy = _proxyGenerator.CreateClassProxy<TAuditable>(auditableInterceptor);
			_auditedObjects.Add(proxy, auditableInterceptor);

			return proxy;
		}

		public static Dictionary<string, IDelta<Object>> GetChanges<T>(this T obj)
		{
			return _auditedObjects[obj].GetChanges();
		}

		public static void ExcludeProperty<TObject, TProperty>(this TObject obj, Expression<Func<TObject, TProperty>> expression)
		{
			PerformIsAuditableCheck(obj);

			var member = expression.Body as MemberExpression;
			if (member != null)
			{
				_auditedObjects[obj].ExcludeProperty(expression);
				
				return;
			}

			throw new ArgumentException();
		}

		public static bool IsAuditable(this object obj)
		{
			return obj != null && _auditedObjects.ContainsKey(obj);
		}

		public static void StartTrackingChanges(this object auditedObject)
		{
			PerformIsAuditableCheck(auditedObject);

			_auditedObjects[auditedObject].StartTrackingChanges();
		}

		public static void StopTrackingChanges(this object auditedObject)
		{
			PerformIsAuditableCheck(auditedObject);

			_auditedObjects[auditedObject].StopTrackingChanges();
		}

		public static bool IsBeingAudited(object theObject)
		{
			return IsAuditable(theObject) && _auditedObjects[theObject].IsBeingAudited();
		}

		private static void PerformIsAuditableCheck<TObject>(TObject obj)
		{
			if (!IsAuditable(obj))
				throw new NotBeingAuditedException(obj);
		}
	}

	public class NotBeingAuditedException : Exception
	{
		public NotBeingAuditedException(object theObject)
		{
		}
	}
}
