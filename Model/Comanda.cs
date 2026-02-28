using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace SorveteriaMaui.Model
{
    [Table("Comanda")]
    public class Comanda
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Indexed(Name = "idx_comanda_status")]
        public int Numero { get; set; }

        public string NomeCliente { get; set; }

        public int Status { get; set; } // 0 = Aberta, 1 = Fechada, 2 = Cancelada

        public DateTime DataAbertura { get; set; }

        public DateTime? DataFechamento { get; set; }

        public double Subtotal { get; set; } = 0;

        public double AcrescimoManual { get; set; } = 0;

        public double DescontoManual { get; set; } = 0;

        public double Total { get; set; } = 0;

        [Indexed(Name = "idx_comanda_sincronizado")]
        public int Sincronizado { get; set; } = 0;
    }
}
