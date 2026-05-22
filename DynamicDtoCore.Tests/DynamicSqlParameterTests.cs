using DynamicDtoCore;
using Xunit;

namespace DynamicDtoCore.Tests
{
    /// <summary>
    /// Enum para testes de parâmetros.
    /// </summary>
    [Flags]
    public enum TestFlags { None = 0, Flag1 = 1, Flag2 = 2, Flag3 = 4 }

    /// <summary>
    /// Testes para processamento de parâmetros SQL na DynamicClassFactory.
    /// Valida conversão de tipos, tratamento de nulidade, arrays e enumerados.
    /// </summary>
    public class DynamicSqlParameterTests : IDisposable
    {
        private DynamicClassFactory? _factory;

        public void Dispose()
        {
            _factory?.Dispose();
        }



        #region Enum Parameter Tests


        #endregion
    }
}