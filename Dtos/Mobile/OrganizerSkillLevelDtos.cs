namespace DropInBadAPI.Dtos
{
    public record SkillLevelDto(int SkillLevelId, byte LevelRank, string LevelName, string ColorHexCode);
    public record CreateSkillLevelDto(byte LevelRank, string LevelName, string ColorHexCode);
    public record UpdateSkillLevelDto(byte LevelRank, string LevelName, string ColorHexCode);
}