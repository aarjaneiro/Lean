﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using System;
using QuantConnect.Util;

namespace QuantConnect.Optimizer
{
    public class ExtremumConverter : TypeChangeJsonConverter<Extremum, string>
    {
        protected override string Convert(Extremum value)
        {
            return value.GetType() == typeof(Maximization)
                ? "max"
                : "min";
        }

        protected override Extremum Convert(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "max": return new Maximization();
                case "min": return new Minimization();
                default:
                    throw new InvalidOperationException("ExtremumConverter.Convert: could not recognize target direction");
            }
        }
    }
}
