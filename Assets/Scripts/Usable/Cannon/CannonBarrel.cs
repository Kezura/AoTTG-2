﻿using System;
using UnityEngine;

namespace Cannon
{
    internal sealed class CannonBarrel
    {
        private readonly Transform barrel;
        private readonly CannonBall.Factory cannonBallFactory;
        private readonly Transform firePoint;
        private readonly Settings settings;
        private float lastFireTime;

        public CannonBarrel(
            Settings settings,
            CannonBall.Factory cannonBallFactory,
            Transform barrel,
            Transform firePoint)
        {
            this.settings = settings;
            this.cannonBallFactory = cannonBallFactory;
            this.barrel = barrel;
            this.firePoint = firePoint;
        }

        private bool CooldownActive =>
            Time.time < lastFireTime + settings.Cooldown;

        public void Rotate(float degrees)
        {
            barrel.Rotate(0f, 0f, degrees * Time.deltaTime);
        }

        public void TryFire(int heroViewID)
        {
            if (!CooldownActive)
                Fire(heroViewID);
        }

        private void Fire(int heroViewID)
        {
            var velocity = firePoint.forward * settings.Force;
            cannonBallFactory.Create(heroViewID, velocity, firePoint.position,
                firePoint.rotation, 0);
            lastFireTime = Time.time;
        }

        [Serializable]
        public class Settings
        {
            public float Cooldown = 2f;
            public float Force = 300f;
            public float TrajectoryLengthFactor = 3f;
        }
    }
}