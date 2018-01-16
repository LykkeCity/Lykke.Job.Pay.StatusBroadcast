namespace Lykke.Job.Pay.StatusBroadcast.Core
{
    public enum BroadcastType
    {
        Order,
        Transfer
    }

    public enum BroadcastMessageType
    {
        Success,
        Process,
        Error
    }
}
