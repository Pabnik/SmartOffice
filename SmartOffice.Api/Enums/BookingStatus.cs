namespace SmartOffice.Api.Enums
{
    public enum BookingStatus
    {
        Active = 0,          // Booking created, waiting for time
        CheckedIn = 1,       // User confirmed presence
        CancelledByUser = 2, // User cancelled
        AutoCancelled = 3    // System cancelled due to no-show (for analytics) 
    }
}
