namespace HlidacStatu.JobsWeb.Models
{
    public class JobPrecalculated
    {
        public string SmlouvaId { get; set; }
        public string IcoOdberatele { get; set; }
        public string[] IcaDodavatelu { get; set; }
    
        public int Year { get; set; }
    
        public long JobPk { get; set; }
        public string JobGrouped { get; set; }
        public decimal SalaryMd { get; set; }
        public decimal SalaryMdVat { get; set; }
        public string Subject { get; set; }
        public string[] Tags { get; set; }
        
        
    }
}