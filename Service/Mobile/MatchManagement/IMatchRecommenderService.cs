using DropInBadAPI.Dtos;

namespace DropInBadAPI.Interfaces
{
    public interface IMatchRecommenderService
    {
        Task<List<RecommendedMatchDto>> SuggestMatchesAsync(int sessionId, SuggestionCriteria criteria);
    }

    public enum SuggestionCriteria
    {
        ByWaitTime,
        ByBalancedSkill,
        Mixed
    }
}