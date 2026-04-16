using System;

namespace DropInBadAPI.Dtos
{
    public record NotificationDto
    {
        public int NotificationId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? ReferenceId { get; init; }
        public bool IsRead { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}