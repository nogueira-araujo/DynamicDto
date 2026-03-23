using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDtoCore
{
    #region Documentation
    /// <summary>
    /// Atributo que define a um método que retorne um tipo dinâmico de <see cref="DynamicClassFactory.Select"/>, os parâmetros customizados para criação
    /// da classe de retorno dinâmica.
    /// </summary>
    #endregion
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class DynamicClassAttribute : Attribute
    {
        #region Properties
        public string ClassName
        { get; private set; }

        public string Namespace
        { get; private set; }
        #endregion

        #region Constructors

        public DynamicClassAttribute(string className)
        {
            if (string.IsNullOrWhiteSpace(className)) throw new ArgumentNullException("className");
            this.ClassName = className;
        }

        public DynamicClassAttribute(string nameSpace, string className)
            : this(className)
        {
            this.Namespace = nameSpace;
        }

        #endregion

        #region Static Methods
        #region Documentation
        /// <summary>
        /// Verifica se o atributo foi atribuido a um membro de classe.
        /// </summary>
        /// <param name="memberInfo">membro a ser analisado.</param>
        /// <returns>Definido=true</returns>
        #endregion
        public static bool IsDefined(MemberInfo memberInfo)
        {
            return Attribute.IsDefined(memberInfo, typeof(DynamicClassAttribute));
        }

        public static DynamicClassAttribute GetDefinedAttribute(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo.GetCustomAttributes(typeof(DynamicClassAttribute), true);
            if (attributes.Length == 0)
            {
                return null;
            }
            else
            {
                return attributes[0] as DynamicClassAttribute;
            }

        }

        /*public static DynamicClassAttribute GetDefinedAttribute<T>(T memberInfo) where T:MemberInfo
        {
            return GetDefinedAttribute(memberInfo);
        }*/
        #endregion Static Methods
    }
}
