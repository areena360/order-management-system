namespace OMS_Backend.DTOs;
public record ChatUnreadDto(int OrderId, string Channel, int Count);
public record MarkChatReadDto(string Channel, int LastReadMessageId);
