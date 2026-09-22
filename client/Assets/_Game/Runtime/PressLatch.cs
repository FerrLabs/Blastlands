namespace Blastlands.Runtime
{
    public sealed class PressLatch
    {
        private bool pressed;

        public void Note(bool pressedThisFrame)
        {
            if (pressedThisFrame)
            {
                pressed = true;
            }
        }

        public bool Take()
        {
            bool was = pressed;
            pressed = false;
            return was;
        }
    }
}
