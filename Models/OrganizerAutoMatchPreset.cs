using System;

namespace DropInBadAPI.Models;

/// <summary>
/// Preset น้ำหนักสำหรับ Auto Match scoring เก็บแยกตามผู้จัดแต่ละคน
/// (ก่อนหน้าเก็บใน SharedPreferences ของเครื่อง — ทำให้ผู้จัดเปลี่ยนเครื่องแล้วต้องตั้งใหม่)
/// </summary>
public partial class OrganizerAutoMatchPreset
{
    public int UserId { get; set; }

    public int QueuePositionMultiplier { get; set; } = 10;
    public int MatchTogetherPenaltyPerOccurrence { get; set; } = 40;
    public int MixedModeOppositeSkillMultiplier { get; set; } = 15;
    public int MixedModeTeammateSkillMultiplier { get; set; } = 20;
    public int SameLevelSkillMultiplier { get; set; } = 30;
    public int TeamFormationTeammateHistoryMultiplier { get; set; } = 2;
    public int TeamFormationOpponentHistoryMultiplier { get; set; } = 1;

    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public virtual User User { get; set; } = null!;
}
