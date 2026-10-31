namespace PurpleGlove
{
    /// <summary>The delivery packet uses prop hover, but clicking accepts it instead of inspecting it.</summary>
    public sealed class SealedPacketAction : SceneObjectAction
    {
        public SealedDeliveryController delivery;
        public override void Activate()
        {
            if (delivery) delivery.CollectPacket();
        }
    }
}
