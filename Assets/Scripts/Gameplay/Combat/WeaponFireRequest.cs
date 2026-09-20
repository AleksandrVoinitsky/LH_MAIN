using System;

namespace LH.Main.Unity.Gameplay
{
    public readonly struct WeaponFireRequest
    {
        public WeaponFireRequest(Guid requestId)
        {
            RequestId = requestId;
        }

        public Guid RequestId { get; }
    }
}
