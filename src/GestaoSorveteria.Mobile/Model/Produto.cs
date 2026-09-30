using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace SorveteriaMaui.Model
{
    [Table("Produto")]
    public class Produto
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Nome { get; set; }

        public int Tipo { get; set; } // 0 = Sorvete, 1 = Açaí, 2 = Bebida

        public double Preco { get; set; }

        public int Ativo { get; set; } = 1;

        public DateTime DataCriacao { get; set; }

        public DateTime? DataAtualizacao { get; set; }
    }
}
