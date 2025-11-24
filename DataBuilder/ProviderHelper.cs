using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBuilder
{
    public static class ProviderHelper
    {
        private static object locker;
        public static string QuotePrefix
        {
            get;
            private set;
        }

        public static string QuoteSufix
        {
            get;
            private set;
        }

        private static DbProviderFactory factory;

        public static DbProviderFactory Factory
        {
            get { return ProviderHelper.factory; }
        }
        public static ConnectionStringSettings ConnStringSetting
        {
            get;
            private set;
        }

        static ProviderHelper()
        {
            try
            {
                locker = new object();
                string stdConnName = System.Configuration.ConfigurationManager.AppSettings["connection"];
                ConnStringSetting = System.Configuration.ConfigurationManager.ConnectionStrings[stdConnName];
                factory = DbProviderFactories.GetFactory(ConnStringSetting.ProviderName);
                DbCommandBuilder commBuilder = factory.CreateCommandBuilder();
                QuotePrefix = commBuilder.QuotePrefix;
                QuoteSufix = commBuilder.QuoteSuffix;
                
            }
            catch (ConfigurationErrorsException confExcept)
            {
                throw new ConfigurationErrorsException("The configuration file probably don't well know formated.", confExcept);
            }
            catch (Exception except)
            {
                throw new ConfigurationErrorsException("The app.config or web.config, dont't be configured correctly or connstring don't supplyed by code.", except);
            }
        }

        public static DbConnection CreateConnection()
        {
            lock(locker)
            {
                DbConnection conn = factory.CreateConnection();
                conn.ConnectionString = ConnStringSetting.ConnectionString;
                return conn;
            }
        }
    }
}
