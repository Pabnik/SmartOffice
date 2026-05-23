namespace SmartOffice.Api.Enums
{
    [Flags]
    public enum Amenities
    {
        None = 0,
        Projector = 1 << 0,
        Whiteboard = 1 << 1,
        Monitor = 1 << 2,
    }
}
