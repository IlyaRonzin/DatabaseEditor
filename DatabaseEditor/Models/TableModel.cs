namespace DatabaseEditor.Models
{
    public class TableModel
    {
        public string Name { get; set; }
        public string PrimaryKeyField { get; set; }
        public List<FieldModel> Fields { get; set; } = new List<FieldModel>();
    }

    public class FieldModel
    {
        public string Name { get; set; }
        public string Type { get; set; } // "int", "double", "text", "timestamp"
    }
}