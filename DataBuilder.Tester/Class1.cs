using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBuilder.Tester
{

    public interface IOrder
    {
        string OvItem { get; set; }
        event Action OnChange;
        void MakeMeVoid();
        int JustDoIt(int it);
    }

    public class Class1
    {
        //[DynamicClass("NomeDaClasse")]
        public static void Main()
        {
            string sql = @"SELECT
          TRIM(O.OV_ITEM) as ""OvItem"", 
          NVL(O.PESO_PEDIDO_NOMINAL, 0) as ""OrdPesoPedidoNominal"",
          case when O.TOL_MAXIMA is null then 1 else (O.tol_maxima / 1000) end as ""OrdTolMaxima"", 
          TRIM(O.ID_PRODUTO) as ""OrdIdProduto"",
          
          TRIM(NVL(C.COD_CARACT, '')) as ""CaracCodCaract"",
          TRIM(NVL(C.VALOR, '')) as ""CaracValor"",
          TRIM(C.TIPO) as ""CaracTipo"",
          C.INTEIROS as ""CaracInteiros"",
          C.DECIMAIS as ""CaracDecimais""
        FROM
          SAP_SALES_ORDER O inner join SAP_SALES_ORDER_CHAR C on O.OV_ITEM = C.OV_ITEM
          
          where
          O.OV_ITEM in (select OV_ITEM from(
                          select count(*), OV_ITEM from SAP_SALES_ORDER_CHAR 
                          where rtrim(COD_CARACT) in ('PP_PRODUTO_BASICO', 'PP_GRUPO_PRODUTO', 'PP_FORMA', 'PP_CODIGO_ESPECIFICACAO', 'PN_USO', 'PP_BLOQUEIO_OV_ITEM', 'PP_MOTIVO_ORDEM')
                          group by OV_ITEM having count(*) >= 7
                          ) T)
order by O.OV_ITEM, C.COD_CARACT";

            //#region Totalmente Dynamic

            //using (DbConnection conn = ProviderHelper.CreateConnection())
            //{
            //    conn.Open();
            //    var factory = new DynamicDataFactory(conn.CreateCommand());

            //    var result = factory.Select(sql);

            //    foreach (var r in result)
            //    {
            //        Console.WriteLine("{0} {1}", r.GetType().Name, r.OvItem);
            //    }

            //}
            //#endregion

            #region Implementação dinamica de interface

            using (DbConnection conn2 = ProviderHelper.CreateConnection())
            {
                conn2.Open();
                var factory = new DynamicDataFactory(conn2.CreateCommand());

                var result2 = factory.Select<IOrder>(sql);

                foreach(var r in result2)
                {
                    Console.WriteLine("{0} {1}", r.GetType().Name, r.OvItem);
                }

            }
            Console.ReadLine();

            #endregion
        }
    }
}
