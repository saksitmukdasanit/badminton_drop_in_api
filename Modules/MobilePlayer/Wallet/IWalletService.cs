using System.Threading.Tasks;
using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetMyWalletAsync(int userId);
        Task<(bool Success, string Message)> WithdrawAsync(int userId, decimal amount);
    }
}