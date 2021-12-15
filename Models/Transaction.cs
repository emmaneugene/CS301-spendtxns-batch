using System;

namespace CS301_batch.Models
{
    public class Transaction
    {
        public string id { get; set; }
        public string transaction_id { get; set; }
        public string merchant { get; set; }
        public string mcc { get; set; }
        public string currency { get; set; }
        public string amount { get; set; }
        public string transaction_date { get; set; }
        public string card_id { get; set; }
        public string card_pan { get; set; }
        public string card_type { get; set; }
    }
}