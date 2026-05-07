namespace DropInBadAPI.Constants
{
    public static class ParticipantTypes
    {
        public const string Member = "Member";
        public const string Guest = "Guest";

        public static bool IsMember(string? value) =>
            string.Equals(value, Member, StringComparison.OrdinalIgnoreCase);

        public static bool IsGuest(string? value) =>
            string.Equals(value, Guest, StringComparison.OrdinalIgnoreCase);

        public static bool IsMemberOrGuest(string? value) => IsMember(value) || IsGuest(value);
    }
}