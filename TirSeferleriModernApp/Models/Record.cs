namespace TirSeferleriModernApp.Models
{
    public class Record
    {
        public int id { get; set; }
        public string? remote_id { get; set; }
        public long updated_at { get; set; }
        public bool is_dirty { get; set; }
        public bool deleted { get; set; }
        public string? containerNo { get; set; }
        public string? loadLocation { get; set; }
        public string? unloadLocation { get; set; }
        public string? size { get; set; }
        public string? status { get; set; }
        public string? nightOrDay { get; set; }
        public string? truckPlate { get; set; }
        public string? notes { get; set; }
        public string? createdByUserId { get; set; }
        public long createdAt { get; set; }
    }
}
