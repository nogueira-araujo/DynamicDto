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

        const string FIELD_PREFIX = "m_";
        const string GET_PREFIX = "get_";
        const string SET_PREFIX = "set_";

        private static Dictionary<string, Type> dynamicTypes;

        private DbCommand command;
        #endregion

        #region Constructors

        static DynamicClassFactory()
        {
            dynamicTypes = new Dictionary<string, Type>();
        }

        public DynamicClassFactory(DbCommand command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

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
            Type dynamicType = BuildDynamicType(schema, trace);
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
                    //param.ParameterName = config.ParameterPrefix + "p" + i.ToString();
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
        private Type BuildDynamicType(DataTable schema, StackTrace trace)
        {
            var attrib = trace.GetFrames().Where(f => Dclass.IsDefined(f.GetMethod())).Select<StackFrame, Dclass>(f => Dclass.GetDefinedAttribute(f.GetMethod())).LastOrDefault();

            const string ASSEMBLY_FORMAT = "{0}.Dynamics";
            const string TYPE_FORMAT = "{0}.{1}.{2}By";
            var callerFrame = trace.GetFrame(1);

            string assemblyName;
            string typeName;

            MethodBase callerMethod = trace.GetFrames().Where(f => f.GetMethod().DeclaringType.Assembly.GetName().Name != "DataBuilder").First().GetMethod();

            if (attrib != null && !string.IsNullOrWhiteSpace(attrib.Namespace))
            {
                assemblyName = string.Format(ASSEMBLY_FORMAT, attrib.Namespace);
            }
            else
            {
                //assemblyName = string.Format(ASSEMBLY_FORMAT, callerFrame.GetMethod().DeclaringType.Assembly.GetName().Name);
                assemblyName = string.Format(ASSEMBLY_FORMAT, callerMethod.DeclaringType.Assembly.GetName().Name);
            }
            if (attrib != null)
            {
                typeName = attrib.ClassName;
            }
            else
            {
                StringBuilder typeNameBuilder = new StringBuilder();
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
                typeName = typeNameBuilder.ToString();
            }


            if (!dynamicTypes.ContainsKey(typeName))
            {
                AssemblyName aName = new AssemblyName(assemblyName);
                //AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);

                AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
                ModuleBuilder mb = builder.DefineDynamicModule(aName.Name);
                TypeBuilder tb = mb.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Serializable | TypeAttributes.UnicodeClass | TypeAttributes.Sealed | TypeAttributes.AutoLayout);
                var cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
                //criando os campos
                List<FieldBuilder> fields = new List<FieldBuilder>();
                foreach (var col in schema.Columns.Cast<DataColumn>())
                {
                    Type targetType = (col.AllowDBNull && col.DataType != typeof(string)) ? typeof(Nullable<>).MakeGenericType(col.DataType) : col.DataType;
                    fields.Add(tb.DefineField(FIELD_PREFIX + col.ColumnName, targetType, FieldAttributes.Private));
                }

                //criando as propriedades para os campos
                List<PropertyBuilder> properties = new List<PropertyBuilder>();
                foreach (var f in fields)
                {
                    properties.Add(tb.DefineProperty(f.Name.Remove(0, FIELD_PREFIX.Length), System.Reflection.PropertyAttributes.None, CallingConventions.Any, f.FieldType, null));
                }

                //definindo getters para as propriedades.
                MethodAttributes getAttrib = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
                foreach (var p in properties)
                {
                    var newMethod = tb.DefineMethod(GET_PREFIX + p.Name, getAttrib, p.PropertyType, Type.EmptyTypes);
                    ILGenerator ilGen = newMethod.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldfld, fields.Where(s => s.Name == (FIELD_PREFIX + p.Name)).FirstOrDefault()); //selecionando o campo de referência.
                    ilGen.Emit(OpCodes.Ret);

                    //atrelando o método à propriedade.
                    p.SetGetMethod(newMethod);
                }
                ConstructorInfo objCtor = typeof(object).GetConstructor(Type.EmptyTypes);

                var ctorIL = cb.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, objCtor);
                ctorIL.Emit(OpCodes.Ret);

                Type dType = tb.CreateType();

                dynamicTypes.Add(typeName, dType);
            }

            return dynamicTypes[typeName];
        }

        #region Documentation
        /// <summary>
        /// Constroi e registra o tipo dinâmico segundo a chamada.
        /// </summary>
        #endregion
        private Type BuildDynamicType<Interface>(DataTable schema, StackTrace trace)
        {
            var attrib = trace.GetFrames().Where(f => Dclass.IsDefined(f.GetMethod())).Select<StackFrame, Dclass>(f => Dclass.GetDefinedAttribute(f.GetMethod())).LastOrDefault();

            const string ASSEMBLY_FORMAT = "{0}.Dynamics";
            const string TYPE_FORMAT = "{0}.{1}.{2}By";
            var callerFrame = trace.GetFrame(1);
            Type interfaceType = typeof(Interface);

            string assemblyName;
            string typeName;
            StringBuilder typeNameBuilder = new StringBuilder(interfaceType.Name.Substring(1) + "Class");

            MethodBase callerMethod = trace.GetFrames().Where(f => f.GetMethod().DeclaringType.Assembly.GetName().Name != "DataBuilder").First().GetMethod();
            //namespace
            if (attrib != null && !string.IsNullOrWhiteSpace(attrib.Namespace))
            {
                assemblyName = string.Format(ASSEMBLY_FORMAT, attrib.Namespace);
            }
            else
            {
                //assemblyName = string.Format(ASSEMBLY_FORMAT, callerFrame.GetMethod().DeclaringType.Assembly.GetName().Name);
                assemblyName = string.Format(ASSEMBLY_FORMAT, callerMethod.DeclaringType.Assembly.GetName().Name);
            }
            //classe
            

            if (attrib != null)
            {
                typeNameBuilder.Insert(0, attrib.ClassName);
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
                typeName = typeNameBuilder.ToString();
            }


            typeName = typeNameBuilder.ToString();

            if (!dynamicTypes.ContainsKey(typeName))
            {
                AssemblyName aName = new AssemblyName(assemblyName);

                //AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);

                AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
                ModuleBuilder mb = builder.DefineDynamicModule(aName.Name);
                TypeBuilder tb = mb.DefineType(typeName, TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Serializable | TypeAttributes.UnicodeClass | /*TypeAttributes.Sealed |*/ TypeAttributes.AutoLayout);
                var cb = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

                List<PropertyBuilder> createdProperties;
                this.MakeInterfaceProperties<Interface>(ref tb,out createdProperties);
                this.MakeQueryProperties(schema,ref tb, createdProperties);
                ConstructorInfo objCtor = typeof(object).GetConstructor(Type.EmptyTypes);

                var ctorIL = cb.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, objCtor);
                ctorIL.Emit(OpCodes.Ret);

                Type dType = tb.CreateType();

                dynamicTypes.Add(typeName, dType);
            }

            return dynamicTypes[typeName];
        }

        private void MakeQueryProperties(DataTable schema,ref TypeBuilder tb, List<PropertyBuilder> sharedProperties)
        {
            Dictionary<string, FieldBuilder> queryFields = new Dictionary<string, FieldBuilder>();
            List<PropertyBuilder> properties = new List<PropertyBuilder>();
            foreach (var col in schema.Columns.Cast<DataColumn>().Where(c => !sharedProperties.Select(s => s.Name).Contains(c.ColumnName)))
            {
                Type targetType = (col.AllowDBNull && col.DataType != typeof(string)) ? typeof(Nullable<>).MakeGenericType(col.DataType) : col.DataType;
                queryFields.Add(FIELD_PREFIX + col.ColumnName, tb.DefineField(FIELD_PREFIX + col.ColumnName, targetType, FieldAttributes.Private));
            }

            foreach (var f in queryFields.Values)
            {
                properties.Add(tb.DefineProperty(f.Name.Remove(0, FIELD_PREFIX.Length), System.Reflection.PropertyAttributes.None, CallingConventions.Any, f.FieldType, null));
            }

            //definindo getters para as propriedades.
            MethodAttributes getAttrib = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            foreach (var p in properties)
            {
                FieldBuilder field = queryFields[FIELD_PREFIX + p.Name];
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
                createdProperties.Add(tb.DefineProperty(f.Name.Remove(0, FIELD_PREFIX.Length), System.Reflection.PropertyAttributes.None, CallingConventions.Any, f.FieldType, null));
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

                    //por ser interface, override
                    tb.DefineMethodOverride(newGet, getMethod);
                }
                if (setMethod != null)
                {
                    var newSet = tb.DefineMethod(SET_PREFIX + p.Name, setAttrib, typeof(void), new Type[] { pinfo.PropertyType });
                    ILGenerator ilGen = newSet.GetILGenerator();
                    ilGen.Emit(OpCodes.Ldarg_0);
                    ilGen.Emit(OpCodes.Ldarg_1);
                    ilGen.Emit(OpCodes.Stfld, field); //selecionando o campo de referência.
                    ilGen.Emit(OpCodes.Ret);

                    //por ser interface, override
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
