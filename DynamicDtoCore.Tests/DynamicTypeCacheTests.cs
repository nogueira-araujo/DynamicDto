using DynamicDtoCore;
using Xunit;
using System.Data;

namespace DynamicDtoCore.Tests
{
    /// <summary>
    /// Testes para cache de tipos dinâmicos da DynamicClassFactory.
    /// Valida reutilização, performance e consistência do cache.
    /// </summary>
    public class DynamicTypeCacheTests : IDisposable
    {
        private DynamicClassFactory? _factory;

        public void Dispose()
        {
            _factory?.Dispose();
        }

        #region Cache Reuse Tests

        

        #endregion
    }
}