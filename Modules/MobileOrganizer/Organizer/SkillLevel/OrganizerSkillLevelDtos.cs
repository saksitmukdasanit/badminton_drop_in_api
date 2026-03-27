namespace DropInBadAPI.Dtos
{
    public record SkillLevelDto(int SkillLevelId, short LevelRank, string LevelName, string ColorHexCode);
    public record SaveSkillLevelDto(int? SkillLevelId, short LevelRank, string LevelName, string ColorHexCode);

}