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

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantConnect.Optimizer
{
    public class Target
    {
        [JsonProperty("target")]
        public string Objective { get; }

        /// <summary>
        /// Defines the direction of optimization, i.e. maximization or minimization
        /// </summary>
        [JsonProperty("extremum")]
        public Extremum Extremum { get; }

        /// <summary>
        /// Current value
        /// </summary>
        public decimal? Current { get; private set; }

        /// <summary>
        /// Target value; if defined and backtest complies with the targets then finish
        /// </summary>
        [JsonProperty("target-value")]
        public decimal? TargetValue { get; }

        /// <summary>
        /// Fires when target complies specified value
        /// </summary>
        public event EventHandler Reached;

        public Target(string target, Extremum extremum, decimal? targetValue)
        {
            var _objective = target;
            if (!_objective.Contains("."))
            {
                // default path
                _objective = $"Statistics.{_objective}";
            }
            // escape empty space in json path
            Objective = string.Join(".", _objective.Split('.').Select(s => $"['{s}']"));

            Extremum = extremum;
            TargetValue = targetValue;
        }

        /// <summary>
        /// Pretty representation of this optimization target
        /// </summary>
        public override string ToString()
        {
            if (TargetValue.HasValue)
            {
                return $"Target: {Objective} TargetValue: {TargetValue.Value} at: {Current}";
            }
            return $"Target: {Objective} at: {Current}";
        }

        public bool MoveAhead(string jsonBacktestResult)
        {
            var computedValue = JObject.Parse(jsonBacktestResult).SelectToken(Objective).Value<string>().ToNormalizedDecimal();
            if (!Current.HasValue || Extremum.Better(Current.Value, computedValue))
            {
                Current = computedValue;
                if (IsComplied())
                {
                    Reached?.Invoke(this, EventArgs.Empty);
                }

                return true;
            }

            return false;
        }

        private bool IsComplied() => TargetValue.HasValue && Current.HasValue && (TargetValue.Value == Current.Value || Extremum.Better(TargetValue.Value, Current.Value));
    }
}
