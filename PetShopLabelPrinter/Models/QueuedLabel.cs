namespace PetShopLabelPrinter.Models
{
    public class QueuedLabel
    {
        public Product Product { get; set; } = new Product();
        public int Quantity { get; set; }
    }
}
