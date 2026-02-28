using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace SorveteriaMaui.Model
{
    [Table("Pagamento")]
    public class Pagamento
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Indexed(Name = "idx_pagamento_comanda")]
        public string ComandaId { get; set; }

        public int Tipo { get; set; } // 0 = Dinheiro, 1 = Cartão, 2 = Pix

        public double Valor { get; set; }

        public DateTime DataPagamento { get; set; }
    }

    [Table("LogSincronizacao")]
    public class LogSincronizacao
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime DataExecucao { get; set; }

        public int Sucesso { get; set; }

        public string Mensagem { get; set; }
    }
}
