﻿using System;
using System.ComponentModel;


namespace Shiny.Power
{
    public class PowerManagerImpl : IPowerManager
    {
        public PowerState Status => PowerState.Charged;
        public int BatteryLevel => 100;

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
