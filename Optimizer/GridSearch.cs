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
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Optimizer
{
    /// <summary>
    /// Defines the way of enumerating all possible candidates for the solution for given optimization parameters settings
    /// </summary>
    public class GridSearch : IOptimizationParameterSetGenerator
    {
        /// <summary>
        /// Enumerate all possible arrangements
        /// </summary>
        /// <param name="seed">Seeding</param>
        /// <param name="args"></param>
        /// <returns>Collection of possible combinations for given optimization parameters settings</returns>
        public IEnumerable<ParameterSet> Step(ParameterSet seed, HashSet<OptimizationParameter> args)
        {
            int i = 0;
            foreach (var step in Recursive(seed, args))
            {
                yield return new ParameterSet(
                    ++i,
                    step.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToStringInvariant()));
            }
        }

        private IEnumerable<Dictionary<string, decimal>> Recursive(ParameterSet seed, HashSet<OptimizationParameter> args)
        {
            if (args.Count == 1)
            {
                var d = args.First();
                for (var value = d.MinValue; value <= d.MaxValue; value += d.Step)
                {
                    yield return new Dictionary<string, decimal>()
                    {
                        {d.Name, value}
                    };
                }
                yield break;
            }

            var d2 = args.First();
            for (var value = d2.MinValue; value <= d2.MaxValue; value += d2.Step)
            {
                foreach (var inner in Recursive(seed, args.Where(s => s.Name != d2.Name).ToHashSet()))
                {
                    inner.Add(d2.Name, value);

                    yield return inner;
                }
            }
        }
    }
}
