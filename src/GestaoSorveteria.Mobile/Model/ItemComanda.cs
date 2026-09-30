using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace SorveteriaMaui.Model
{
    [Table("ItemComanda")]
    public class ItemComanda
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Indexed(Name = "idx_item_comanda")]
        public string ComandaId { get; set; }

        public string ProdutoId { get; set; }
        public string ProdutoNome { get; set; } // Adicione isso!
        public double Quantidade { get; set; }

        public double PrecoUnitario { get; set; }

        public double Total { get; set; }

        public int Cancelado { get; set; } = 0;
    }
}
