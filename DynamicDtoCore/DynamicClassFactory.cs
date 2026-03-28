using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Dclass = DynamicDtoCore.DynamicClassAttribute;

namespace DynamicDtoCore
{
    #region Documentation
    /// <summary>
    /// Classe reponsável pela transaformação de resultados de consultas em objetos criados dinâmicamente.
    /// </summary>
    #endregion
    [DataObject]
    public class DynamicClassFactory
    {
        #region Fields
        const string ASSEMBLY_FORMAT = "{0}.Dynamics";
        const string TYPE_FORMAT = "{0}.{1}.{2}";
        const string FIELD_PREFIX = "m_";
        const string GET_PREFIX = "get_";
        const string SET_PREFIX = "set_";

        private static ConcurrentDictionary<string, Type> dynamicTypes;
        private static readonly bool useParameterNames = true;
        private static readonly string parameterPrefix = "@";

        private String thisAssemblyName;

        private DbCommand command;
        #endregion

        #region Constructors

        static DynamicClassFactory()
        {
            try
            {
                dynamicTypes = new ConcurrentDictionary<string, Type>();


                DynamicClassFactory.useParameterNames = ConfigurationHelper.UseDbParameterName;
                if (DynamicClassFactory.useParameterNames)
                {
                    DynamicClassFactory.parameterPrefix = ConfigurationHelper.ParameterPrefix;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not initialize DynamicClassFactory.", ex);
            }
        }

        public DynamicClassFactory(DbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            this.thisAssemblyName = this.GetType().Assembly.GetName().Name;
            this.command = command;
        }

        #endregion

        #region Methods

        #region Documentation
        /// <summary>
        /// Executa a consulta passada no parâmetro, segundo os argumentos desejados.
        /// </summary>
        /// <returns>
        /// <see cref="IEnumerable"/> de tipo dinâmico que representa a resposta à consulta 
        /// </returns>
        #endregion
        [DataObjectMethod(DataObjectMethodType.Select)]
        public IEnumerable<Interface> Select<Interface>(string sql, params object[] args)
        {
            DataTable data;
            DataTable schema;
            StackTrace trace;
            sql = InnerSelect(sql, args, out data, out schema, out trace);
            Type dynamicType = BuildDynamicType<Interface>(schema, trace);
            return BuildResponse(dynamicType, data).Cast<Interface>();
        }

        #region Documentation
            /// <summary>
            /// Executa a consulta passada no parâmetro, segundo os argumentos desejados.
            /// </summary>
            /// <returns>
            /// <see cref="IEnumerable"/> de tipo dinâmico que representa a resposta à consulta 
            /// </returns>
            #endregion
        [DataObjectMethod(DataObjectMethodType.Select)]
        public IEnumerable<dynamic> Select(string sql, params object[] args)
        {

            DataTable data;
            DataTable schema;
            StackTrace trace;
            sql = InnerSelect(sql, args, out data, out schema, out trace);
            Type dynamicType = BuildDynamicClass(schema, trace);
            return BuildResponse(dynamicType, data);
        }

        private string InnerSelect(string sql, object[] args, out DataTable data, out DataTable schema, out StackTrace trace)
        {
            data = new DataTable();
            schema = new DataTable();
            trace = new StackTrace();

            this.Prepare(ref sql, args);
            var adapt = ProviderHelper.Factory.CreateDataAdapter();
            adapt.SelectCommand = this.command;
            adapt.Fill(data);
            adapt.FillSchema(schema, SchemaType.Source);
            return sql;
        }

        

        #region Documentation
        /// <summary>
        /// Varre recursivamente os parâmetros passados em busca de arrays em seu interior de forma a formar um array unico de dimensão simples.
        /// </summary>
        #endregion
        private object[] PreProcessArgs(object[] args)
        {
            List<object> result = new List<object>();

            foreach (object a in args)
            {
                if (a == null)
                {
                    result.Add(null);
                }
                else if (a.GetType().IsArray)
                {
                    result.AddRange(PreProcessArgs((object[])a));
                }
                else
                {
                    result.Add(a);
                }
            }
            return result.ToArray();
        }

        #region Documentation
        /// <summary>
        /// Método auxiliar a Prepare, pré-processa o tipo de um parâmetro e redefine-o para seu tipo primitivo em caso de valores enumerados.
        /// </summary>
        /// <remarks>
        /// A redefinição do tipo enumerado ao seu tipo primitivo se deve à incapacidade dos drivers do Oracle em lidar com
        /// tais tipos de forma eficiente, gera erros, o que não acontece com os drivers do Sql Server e MySql por exemplo,
        /// mas que pode vir a acontecer com um outro driver qualquer, embora eu duvide. Enfim, tem coisas que só o Oracle faz pra vc.
        /// </remarks>
        #endregion Documentation
        private void DefineParameter(DbParameter param, object value)
        {
            if (value == null)
            {
                param.Value = DBNull.Value;
            }
            else if (value.GetType().IsEnum)
            {
                Type undelying = Enum.GetUnderlyingType(value.GetType());
                value = Convert.ChangeType(value, undelying);
                param.Value = value;
            }
            else
            {
                param.Value = value;
            }
        }

        #region Documentation
        /// <summary>
        /// Recebe uma instrução sql e os parâmetros que devem ser concatenados a ela e 
        /// prepara o comando para ser executado.
        /// </summary>
        /// <param name="sql">instrução sql.</param>
        /// <param name="args">argumentos a concatenar</param>
        #endregion
        private void Prepare(ref string sql, params object[] args)
        {
            this.command.Parameters.Clear();
            if (args.Length == 0)
            {
                this.command.CommandText = sql;
            }
            else
            {
                object[] values = PreProcessArgs(args);
                List<string> paramsNames = new List<string>();
                for (int i = 0; i < values.Length; i++)
                {
                    if (!sql.Contains("{" + i.ToString() + "}")) throw new IndexOutOfRangeException("DynamicDataFactory.Prepare(sql, args).\nÍndice do argumento inexistente na expressão sql.");
                    DbParameter param = this.command.CreateParameter();
                    if (DynamicClassFactory.useParameterNames)
                    {
                        param.ParameterName = parameterPrefix + "p" + i.ToString();
                    }
                    this.DefineParameter(param, values[i]);
                    paramsNames.Add(param.ParameterName);
                    this.command.Parameters.Add(param);
                }
                try
                {
                    StringBuilder paramParser = new StringBuilder();
                    this.command.CommandText = paramParser.AppendFormat(sql, paramsNames.ToArray()).ToString();
                }
                catch (Exception except)
                {
                    throw new IndexOutOfRangeException("DynamicDataFactory.Prepare(sql, args).\n" + except.Message, except);
                }
            }

        }

        #region Documentation
        /// <summary>
        /// Constroi e registra o tipo dinâmico segundo a chamada.
        /// </summary>
        #endregion
        private Type BuildDynamicClass(DataTable schema, StackTrace trace)
        {
            //neste caso typNameBuilder é passado vazio apenas para tender à estrutura do método ExtractAssemblyAndTypeName.
            string assemblyName, typeName;
            StringBuilder typeNameBuilder = new StringBuilder();
            ExtractAssemblyAndTypeName(trace,string.Empty, out assemblyName, out typeName);

            if (!dynamicTypes.ContainsKey(typeName))
            {
                AssemblyName aName = new AssemblyName(assemblyName);

                AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
                ModuleBuilder mb = builder.DefineDynamicModule(aName.Name);
                TypeBuilder tb = mb.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.UnicodeClass | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
                ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                //criando os campos

                List<PropertyBuilder> createdProperties = new List<PropertyBuilder>();
                tb = MakeType(schema, typeName, tb, cb, createdProperties);
            }

            return dynamicTypes[typeName];
        }

        private void InjectEqualityLogic(ref TypeBuilder tb, List<PropertyBuilder> properties)
        {
            MethodAttributes methodAttrs = MethodAttributes.Public
                                 | MethodAttributes.Virtual
                                 | MethodAttributes.HideBySig;

            var equalsMb = tb.DefineMethod("Equals", methodAttrs, typeof(bool), new Type[] { typeof(object) });
            var il = equalsMb.GetILGenerator();
            Label isNull = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Brfalse_S, isNull);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            MethodInfo method = typeof(DynamicAsRecordHelper).GetMethod("AreEqual");
            if (method is null)
            {
                throw new Exception("AreEqual method not found in DynamicEqualityComparer.");
            }

            MethodInfo generic = method.MakeGenericMethod(tb);

            il.Emit(OpCodes.Call, generic);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(isNull);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(equalsMb, typeof(object).GetMethod("Equals", new Type[] { typeof(object) }));

            MethodBuilder hashMb = tb.DefineMethod(
        "GetHashCode",
        methodAttrs,
        typeof(int),
        Type.EmptyTypes);

            ILGenerator ilHash = hashMb.GetILGenerator();

            // return DynamicEqualityComparer.GetGeneratedHashCode<T>(this);
            ilHash.Emit(OpCodes.Ldarg_0); // Carrega 'this'

            MethodInfo getHashMethod = typeof(DynamicAsRecordHelper)
                .GetMethod("GetGeneratedHashCode")
                .MakeGenericMethod(tb);

            ilHash.Emit(OpCodes.Call, getHashMethod);
            ilHash.Emit(OpCodes.Ret);

            // Vincula explicitamente ao GetHashCode da classe base (System.Object)
            tb.DefineMethodOverride(hashMb, typeof(object).GetMethod("GetHashCode"));
        }

        private void InjectToString(TypeBuilder tb)
        {
            MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig;

            MethodBuilder mb = tb.DefineMethod("ToString", attrs, typeof(string), Type.EmptyTypes);
            ILGenerator il = mb.GetILGenerator();

            // return DynamicAsRecordHelper.Render<GeneratedType>(this);
            il.Emit(OpCodes.Ldarg_0);

            MethodInfo method = typeof(DynamicAsRecordHelper).GetMethod("Render");
            MethodInfo genericMethod = method.MakeGenericMethod(tb);

            il.Emit(OpCodes.Call, genericMethod);
            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(mb, typeof(object).GetMethod("ToString"));
        }

        private void InjectCloneMethod(TypeBuilder tb)
        {
            // Define um método 'public GeneratedType Copy()'
            MethodBuilder mb = tb.DefineMethod("Copy", MethodAttributes.Public | MethodAttributes.HideBySig, tb, Type.EmptyTypes);
            ILGenerator il = mb.GetILGenerator();

            // return DynamicAsRecordHelper.Clone<GeneratedType>(this);
            il.Emit(OpCodes.Ldarg_0);

            MethodInfo method = typeof(DynamicAsRecordHelper).GetMethod("Clone").MakeGenericMethod(tb);
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
        }

        private void InjectDeconstruct(TypeBuilder tb, List<PropertyBuilder> properties)
        {
            if (properties == null || properties.Count == 0)
                return;

            var parameterTypes = properties.Select(p => p.PropertyType.MakeByRefType()).ToArray();

            MethodBuilder mb = tb.DefineMethod(
                "Deconstruct",
                MethodAttributes.Public | MethodAttributes.HideBySig,
                typeof(void),
                parameterTypes);

            // Define os nomes dos parâmetros e marca-os como 'out'
            for (int i = 0; i < properties.Count; i++)
            {
                mb.DefineParameter(i + 1, ParameterAttributes.Out, properties[i].Name.ToLower());
            }

            ILGenerator il = mb.GetILGenerator();

            for (int i = 0; i < properties.Count; i++)
            {
                Type propType = properties[i].PropertyType;
                MethodInfo getMethod = properties[i].GetGetMethod(true);
                if (getMethod == null)
                    throw new InvalidOperationException($"Getter for property '{properties[i].Name}' not found.");

                // Load address of the 'out' parameter (argument index i+1)
                il.Emit(OpCodes.Ldarg, i + 1);

                // Load 'this' and call the getter to push the value
                il.Emit(OpCodes.Ldarg_0);

                // Use Call for value-type getters (no virtual dispatch), Callvirt ok for reference-type getters.
                // Using Call for value-type avoids invalid use of callvirt on value types.
                il.Emit(propType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, getMethod);

                // Store the value into the byref parameter:
                if (propType.IsValueType)
                {
                    // For value types (including Nullable<T>), use stobj <propType>
                    il.Emit(OpCodes.Stobj, propType);
                }
                else
                {
                    // For reference types, use stind.ref
                    il.Emit(OpCodes.Stind_Ref);
                }
            }

            il.Emit(OpCodes.Ret);
        }

        private void ExtractAssemblyAndTypeName(StackTrace trace, string defTypeName, out string assemblyName, out string typeName)
        {
            var frames = trace.GetFrames();
            var methods = frames.Select(f => f.GetMethod());
            var attrib = methods.Where(m => Dclass.IsDefined(m)).Select(m => Dclass.GetDefinedAttribute(m)).FirstOrDefault<Dclass>();
            //var attrib = trace.GetFrames().Where(f => Dclass.IsDefined(f.GetMethod())).Select<StackFrame, Dclass>(f => Dclass.GetDefinedAttribute(f.GetMethod())).LastOrDefault();

            var callerFrame = trace.GetFrame(1);

            StringBuilder typeNameBuilder = new StringBuilder();
            MethodBase callerMethod = trace.GetFrames().Where(f => f.GetMethod().DeclaringType.Assembly.GetName().Name != this.thisAssemblyName).First().GetMethod();

            if (attrib != null && !string.IsNullOrWhiteSpace(attrib.Namespace))
            {
                assemblyName = string.Format(ASSEMBLY_FORMAT, attrib.Namespace);
            }
            else
            {
                assemblyName = string.Format(ASSEMBLY_FORMAT, callerMethod.DeclaringType.Assembly.GetName().Name);
            }
            if (attrib != null)
            {
                typeName = assemblyName + "." + attrib.ClassName;
            }
            else
            {
                typeNameBuilder.AppendFormat(TYPE_FORMAT, assemblyName, callerMethod.DeclaringType.Name, callerMethod.Name);
                
                if (callerMethod.GetParameters().Length > 0)
                {
                    foreach (var a in callerMethod.GetParameters())
                    {
                        var name = a.Name.ToArray();
                        name[0] = Char.ToUpper(name[0]);
                        typeNameBuilder.Append(new string(name));
                    }
                }
                else
                {
                    typeNameBuilder.Append("Void");
                }
                typeNameBuilder.Append("." + defTypeName);
                typeName = typeNameBuilder.ToString();
            }
        }

        #region Documentation
        /// <summary>
        /// Constroi e registra o tipo dinâmico segundo a chamada.
        /// </summary>
        #endregion
        private Type BuildDynamicType<Interface>(DataTable schema, StackTrace trace)
        {
            Type interfaceType = typeof(Interface);

            string assemblyName, typeName;
            ExtractAssemblyAndTypeName(trace, interfaceType.Name.Substring(1) + "Class", out assemblyName, out typeName);

            if (!dynamicTypes.ContainsKey(typeName))
            {
                AssemblyName aName = new AssemblyName(assemblyName);

                AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
                ModuleBuilder mb = builder.DefineDynamicModule(aName.Name);
                TypeBuilder tb = mb.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
                ConstructorBuilder cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

                List<PropertyBuilder> createdProperties;
                this.MakeInterfaceProperties<Interface>(ref tb, out createdProperties);
                tb = MakeType(schema, typeName, tb, cb, createdProperties);
            }

            return dynamicTypes[typeName];
        }

        private TypeBuilder MakeType(DataTable schema, string typeName, TypeBuilder tb, ConstructorBuilder cb, List<PropertyBuilder> createdProperties)
        {
            this.MakeQueryProperties(schema, ref tb, createdProperties);
            this.CreateVoidCtor(cb);
            this.InjectEqualityLogic(ref tb, createdProperties);
            this.InjectToString(tb);
            this.InjectCloneMethod(tb);
            this.InjectDeconstruct(tb, createdProperties);
            Type dType = tb.CreateType();
            dynamicTypes.TryAdd(typeName, dType);
            return tb;
        }

        #region Documentation
        /// <summary>
        /// Cria o construtor padrão para o tipo dinâmico, necessário para a criação de instâncias do tipo posteriormente.
        /// </summary>
        /// <param name="cb">Instância de <see cref="ConstructorBuilder"/>Constructor Builder</param>
        #endregion
        private void CreateVoidCtor(ConstructorBuilder cb)
        {
            ConstructorInfo objCtor = typeof(object).GetConstructor(Type.EmptyTypes);

            var ctorIL = cb.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, objCtor);
            ctorIL.Emit(OpCodes.Ret);
        }

        private void MakeQueryProperties(DataTable schema,ref TypeBuilder tb, List<PropertyBuilder> sharedProperties)
        {
            Dictionary<string, FieldBuilder> fields = new Dictionary<string, FieldBuilder>();
            List<PropertyBuilder> properties = new List<PropertyBuilder>();
            foreach (var col in schema.Columns.Cast<DataColumn>().Where(c => !sharedProperties.Select(s => s.Name).Contains(c.ColumnName)))
            {
                Type targetType = (col.AllowDBNull && col.DataType != typeof(string)) ? typeof(Nullable<>).MakeGenericType(col.DataType) : col.DataType;
                fields.Add(FIELD_PREFIX + col.ColumnName, tb.DefineField(FIELD_PREFIX + col.ColumnName, targetType, FieldAttributes.Private));
            }

            foreach (var f in fields.Values)
            {
                properties.Add(tb.DefineProperty(f.Name.Remove(0, FIELD_PREFIX.Length), System.Reflection.PropertyAttributes.None, CallingConventions.Any, f.FieldType, null));
            }

            //definindo getters para as propriedades.
            MethodAttributes getAttrib = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            foreach (var p in properties)
            {
                FieldBuilder field = fields[FIELD_PREFIX + p.Name];
                var newMethod = tb.DefineMethod(GET_PREFIX + p.Name, getAttrib, p.PropertyType, Type.EmptyTypes);
                ILGenerator ilGen = newMethod.GetILGenerator();
                ilGen.Emit(OpCodes.Ldarg_0);
                ilGen.Emit(OpCodes.Ldfld, field); //selecionando o campo de referência.
                ilGen.Emit(OpCodes.Ret);

                //atrelando o método à propriedade.
                p.SetGetMethod(newMethod);
            }
        }

        private void MakeInterfaceProperties<Interface>(ref TypeBuilder tb,out List<PropertyBuilder> createdProperties)
        {
            Type[] requiredModifiers = new[] { typeof(System.Runtime.CompilerServices.IsExternalInit) };
            createdProperties = new List<PropertyBuilder>();

            tb.AddInterfaceImplementation(typeof(Interface));
            Type iType = typeof(Interface);

            BindingFlags bFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance;

            PropertyInfo[] interfaceProperties = iType.GetProperties();
            MethodInfo[] interfaceMethods = iType.GetMethods(bFlags).Where(m => !m.Name.Contains(GET_PREFIX) && !m.Name.Contains(SET_PREFIX)).ToArray();
            Dictionary<string, FieldBuilder> fields = new Dictionary<string, FieldBuilder>();
            foreach (var f in interfaceProperties)
            {
                fields.Add(FIELD_PREFIX + f.Name, tb.DefineField(FIELD_PREFIX + f.Name, f.PropertyType, FieldAttributes.Private));
            }

            foreach (var f in fields.Values)
            {
                createdProperties.Add(tb.DefineProperty(f.Name.Remove(0, FIELD_PREFIX.Length), PropertyAttributes.None, CallingConventions.Any, f.FieldType, null));
            }

            //definindo getters e setters para as propriedades.
            MethodAttributes getAttrib = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            MethodAttributes setAttrib = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            foreach (var p in createdProperties)
            {
                PropertyInfo pinfo = (from PropertyInfo pi in interfaceProperties where pi.Name == p.Name select pi).FirstOrDefault();
                MethodInfo getMethod = pinfo.GetGetMethod();
                MethodInfo setMethod = pinfo.GetSetMethod();
                FieldBuilder field = fields[FIELD_PREFIX + p.Name];
                if (getMethod != null)
                {

                    var newGet = tb.DefineMethod(GET_PREFIX + p.Name, getAttrib, p.PropertyType, Type.EmptyTypes);
                    ILGenerator ilGen = newGet.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldfld, field); //selecionando o campo de referência.
                    ilGen.Emit(OpCodes.Ret);
                    p.SetGetMethod(newGet);
                    //por ser interface, override
                    tb.DefineMethodOverride(newGet, getMethod);
                }
                if (setMethod != null)
                {
                    // Em vez de usar SetParameters(requiredModifiers)
                    var newSet = tb.DefineMethod(SET_PREFIX + p.Name,
    setAttrib,
    typeof(void),
    new Type[] { pinfo.PropertyType });

                    ILGenerator ilGen = newSet.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldarg_1);
                    ilGen.Emit(OpCodes.Stfld, field);
                    ilGen.Emit(OpCodes.Ret);

                    p.SetSetMethod(newSet);
                    tb.DefineMethodOverride(newSet, setMethod);
                }
            }

            //definindo métodos da interface
            foreach (var methodInfo in interfaceMethods)
            {
                Type returnType = methodInfo.ReturnType;

                Type[] argumentTypes = (from ParameterInfo p in methodInfo.GetParameters() select p.ParameterType).ToArray();
                MethodBuilder methodBuilder = tb.DefineMethod
                    (methodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, returnType, argumentTypes);

                var ilGenerator = methodBuilder.GetILGenerator();

                if (returnType != typeof(void))
                {
                    LocalBuilder localBuilder = ilGenerator.DeclareLocal(returnType);
                    ilGenerator.Emit(OpCodes.Ldloc, localBuilder);
                }

                ilGenerator.Emit(OpCodes.Ret);                       // return
                tb.DefineMethodOverride(methodBuilder, methodInfo);

            }

        }

        #region Documentation
        /// <summary>
        /// Transforma a resposta armazenada no <see cref="DataTable"/> em um enumerável de instâncias do tipo definido.
        /// </summary>
        #endregion
        private IEnumerable<dynamic> BuildResponse(Type dynamicType, DataTable data)
        {
            List<dynamic> dynamics = new List<dynamic>();
            foreach (var row in data.Rows.Cast<DataRow>())
            {
                dynamic dyn = Activator.CreateInstance(dynamicType);
                foreach (var info in dynamicType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var fName = info.Name.Remove(0, FIELD_PREFIX.Length);
                    if (data.Columns.Contains(fName))
                    {
                        if (!(row[fName] is DBNull))
                        {
                            info.SetValue(dyn, row[fName]);
                        }
                        else if (info.FieldType == typeof(string))
                        {
                            info.SetValue(dyn, row[fName] is DBNull ? null : row[fName]);
                        }
                        else if (!(info.FieldType.UnderlyingSystemType.IsGenericType && (info.FieldType.UnderlyingSystemType.GetGenericTypeDefinition() == typeof(Nullable<>))))
                        {
                            const string message = "The field {0} on dynamic type {1} cannot be null.";
                            throw new ArgumentNullException(string.Format(message, fName, info.DeclaringType.Name));
                        }
                    }
                    else
                    {
                        throw new InvalidProgramException();
                    }
                }
                dynamics.Add(dyn);
            }
            return dynamics;
        }

        #endregion
    }
}
