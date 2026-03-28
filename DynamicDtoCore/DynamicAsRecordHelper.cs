using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicDtoCore
{
    public static class DynamicAsRecordHelper
    {
        private static readonly ConcurrentDictionary<Type, Delegate> cache = new();
        private static readonly ConcurrentDictionary<Type, Delegate> hashCache = new();
        private static readonly ConcurrentDictionary<Type, Delegate> toStringCache = new();
        private static readonly ConcurrentDictionary<Type, Delegate> cloneCache = new();


        public static bool AreEqual<T>(T left, T right)
        {
            if (left == null || right == null) return ReferenceEquals(left, right);

            var type = typeof(T);
            var comparer = (Func<T, T, bool>)cache.GetOrAdd(type, t => CompileComparer<T>(t));

            return comparer(left, right);

        }

        public static int GetGeneratedHashCode<T>(T obj)
        {
            if (obj == null) return 0;
            var type = typeof(T);
            var hashFunc = (Func<T, int>)hashCache.GetOrAdd(type, t => CompileHashFunc<T>(t));
            return hashFunc(obj);
        }

        private static Delegate CompileManualHash(Type type, PropertyInfo[] properties, ParameterExpression param)
        {
            // Constantes para o algoritmo de hash
            var seed = Expression.Constant(17);
            var modifier = Expression.Constant(31);

            // Variável local para armazenar o hash acumulado
            var hashVariable = Expression.Variable(typeof(int), "hash");

            var operations = new List<Expression>();

            // Inicializa: hash = 17
            operations.Add(Expression.Assign(hashVariable, seed));

            foreach (var prop in properties)
            {
                var propValue = Expression.Property(param, prop);

                // Coalesce para evitar NullReferenceException: (prop ?? 0).GetHashCode()
                // No caso de tipos de valor, o GetHashCode é direto.
                Expression getPropHash;
                if (prop.PropertyType.IsValueType)
                {
                    getPropHash = Expression.Call(
                        Expression.Convert(propValue, typeof(object)),
                        typeof(object).GetMethod("GetHashCode")
                    );
                }
                else
                {
                    // Se for nulo, usamos 0 como hash
                    getPropHash = Expression.Condition(
                        Expression.Equal(propValue, Expression.Constant(null)),
                        Expression.Constant(0),
                        Expression.Call(propValue, typeof(object).GetMethod("GetHashCode"))
                    );
                }

                // hash = (hash * 31) + getPropHash
                var multiply = Expression.Multiply(hashVariable, modifier);
                var add = Expression.Add(multiply, getPropHash);
                operations.Add(Expression.Assign(hashVariable, add));
            }

            // O último item do bloco é o valor de retorno
            operations.Add(hashVariable);

            // Cria o bloco com a variável local e as operações
            var body = Expression.Block(new[] { hashVariable }, operations);

            return Expression.Lambda(body, param).Compile();
        }

        private static Delegate CompileHashFunc<T>(Type type)
        {
            var param = Expression.Parameter(type, "obj");
            var properties = type.GetProperties();

            // Se não houver propriedades, retorna um hash constante
            if (properties.Length == 0)
                return Expression.Lambda<Func<T, int>>(Expression.Constant(0), param).Compile();

            // No .NET Core/5+, usamos HashCode.Combine
            var combineMethod = typeof(HashCode).GetMethod("Combine",
                properties.Select(p => p.PropertyType).ToArray());

            if (combineMethod != null && properties.Length <= 8)
            {
                // HashCode.Combine aceita até 8 argumentos
                var calls = properties.Select(p => Expression.Property(param, p));
                var body = Expression.Call(combineMethod, calls);
                return Expression.Lambda<Func<T, int>>(body, param).Compile();
            }

            // Fallback para muitos campos: Lógica de acumulação manual via Expression
            return CompileManualHash(type, properties, param);
        }

        private static Delegate CompileComparer<T>(Type type)
        {
            var leftParam = Expression.Parameter(type, "left");
            var rightParam = Expression.Parameter(type, "right");

            // Começamos com 'true' e vamos acumulando com AND (&&)
            Expression body = Expression.Constant(true);

            foreach (var prop in type.GetProperties())
            {
                var leftValue = Expression.Property(leftParam, prop);
                var rightValue = Expression.Property(rightParam, prop);

                // Cria a comparação: left.Prop == right.Prop
                var equal = Expression.Equal(leftValue, rightValue);

                // body = body && (left.Prop == right.Prop)
                body = Expression.AndAlso(body, equal);
            }

            return Expression.Lambda<Func<T, T, bool>>(body, leftParam, rightParam).Compile();
        }


        public static string Render<T>(T obj)
        {
            if (obj == null) return string.Empty;
            var type = typeof(T);
            var formatter = (Func<T, string>)toStringCache.GetOrAdd(type, t => CompileToString<T>(t));
            return formatter(obj);
        }

        private static Delegate CompileToString<T>(Type type)
        {
            var param = Expression.Parameter(type, "obj");
            var nameProp = Expression.Constant($"{type.Name} {{ ");
            var closing = Expression.Constant(" }");
            var separator = Expression.Constant(", ");
            var equalSign = Expression.Constant(" = ");

            var expressions = new List<Expression>();
            expressions.Add(nameProp);

            var props = type.GetProperties();
            var toStringObjMethod = typeof(object).GetMethod("ToString", Type.EmptyTypes);

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var propValue = Expression.Property(param, prop);

                Expression toStringCall;

                var propType = prop.PropertyType;
                var nullableUnderlying = Nullable.GetUnderlyingType(propType);

                if (propType.IsValueType && nullableUnderlying == null)
                {
                    // Non-nullable value type: call ToString() directly (no null check, avoid boxing when possible)
                    toStringCall = Expression.Call(propValue, propType.GetMethod("ToString", Type.EmptyTypes));
                    // Fallback to boxed ToString if specific ToString not found
                    if (toStringCall == null)
                        toStringCall = Expression.Call(Expression.Convert(propValue, typeof(object)), toStringObjMethod);
                }
                else
                {
                    // Reference type or Nullable<T>: compare with a null constant typed as the property type
                    var nullConst = Expression.Constant(null, propType);
                    var whenNull = Expression.Constant("null");

                    // When not null, call ToString on boxed value (works for reference and Nullable<T> when HasValue)
                    var whenNotNull = Expression.Call(Expression.Convert(propValue, typeof(object)), toStringObjMethod);

                    toStringCall = Expression.Condition(
                        Expression.Equal(propValue, nullConst),
                        whenNull,
                        whenNotNull
                    );
                }

                expressions.Add(Expression.Constant(prop.Name));
                expressions.Add(equalSign);
                expressions.Add(toStringCall);

                if (i < props.Length - 1)
                    expressions.Add(separator);
            }

            expressions.Add(closing);

            var concatMethod = typeof(string).GetMethod("Concat", new[] { typeof(object[]) });
            var arrayExpr = Expression.NewArrayInit(typeof(object), expressions.Select(e => Expression.Convert(e, typeof(object))));
            var body = Expression.Call(concatMethod, arrayExpr);

            return Expression.Lambda<Func<T, string>>(body, param).Compile();
        }

        public static T Clone<T>(T source) where T : new()
        {
            if (source == null) return default;
            var type = typeof(T);
            var cloner = (Action<T, T>)cloneCache.GetOrAdd(type, t => CompileCloner<T>(t));

            var target = new T();
            cloner(source, target);
            return target;
        }

        private static Delegate CompileCloner<T>(Type type)
        {
            var sourceParam = Expression.Parameter(type, "source");
            var targetParam = Expression.Parameter(type, "target");
            var operations = new List<Expression>();

            foreach (var prop in type.GetProperties().Where(p => p.CanWrite))
            {
                var sourceValue = Expression.Property(sourceParam, prop);
                var targetProperty = Expression.Property(targetParam, prop);

                // target.Prop = source.Prop
                operations.Add(Expression.Assign(targetProperty, sourceValue));
            }

            var body = Expression.Block(operations);
            return Expression.Lambda<Action<T, T>>(body, sourceParam, targetParam).Compile();
        }

    }
}
