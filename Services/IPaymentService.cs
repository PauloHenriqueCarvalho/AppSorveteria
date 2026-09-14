using SorveteriaMaui.Model;
using System.Threading.Tasks;

namespace SorveteriaMaui.Services
{
    public interface IPaymentService
    {
        Task<bool> ProcessarPagamentoAsync(Comanda comanda);
    }
}
