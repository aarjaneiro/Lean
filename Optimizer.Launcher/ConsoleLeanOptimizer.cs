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

using QuantConnect.Util;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.Optimizer.Launcher
{
    /// <summary>
    /// Optimizer implementation that launches Lean as a local process
    /// TODO: review object store location, believe it's being shared, when the algos end they all try to delete the directory
    /// </summary>
    public class ConsoleLeanOptimizer : LeanOptimizer
    {
        private readonly string _leanLocation;
        private readonly string _rootResultDirectory;
        private readonly bool _closeLeanAutomatically;
        private readonly ConcurrentDictionary<string, Process> _processByBacktestId;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="nodePacket">The optimization node packet to handle</param>
        public ConsoleLeanOptimizer(OptimizationNodePacket nodePacket) : base(nodePacket)
        {
            _processByBacktestId = new ConcurrentDictionary<string, Process>();

            _rootResultDirectory = Configuration.Config.Get("results-destination-folder",
                Path.Combine(Directory.GetCurrentDirectory(), $"opt-{nodePacket.OptimizationId}"));
            Directory.CreateDirectory(_rootResultDirectory);

            _leanLocation = Configuration.Config.Get("lean-binaries-location",
                Path.Combine(Directory.GetCurrentDirectory(), "../../../Launcher/bin/Debug/QuantConnect.Lean.Launcher.exe"));

            _closeLeanAutomatically = Configuration.Config.GetBool("close-automatically", true);
        }

        /// <summary>
        /// Handles starting Lean for a given parameter set
        /// </summary>
        /// <param name="parameterSet">The parameter set for the backtest to run</param>
        /// <returns>The new unique backtest id</returns>
        protected override string RunLean(ParameterSet parameterSet)
        {
            var backtestId = Guid.NewGuid().ToString();

            // start each lean instance in its own directory so they store their logs & results, else they fight for the log.txt file
            var resultDirectory = Path.Combine(_rootResultDirectory, backtestId);
            Directory.CreateDirectory(resultDirectory);

            // Use ProcessStartInfo class
            var startInfo = new ProcessStartInfo
            {
                FileName = _leanLocation,
                WorkingDirectory = Directory.GetParent(_leanLocation).FullName,
                Arguments = $"--results-destination-folder \"{resultDirectory}\" --algorithm-id \"{backtestId}\" --close-automatically {_closeLeanAutomatically} --parameters {parameterSet}",
                WindowStyle = ProcessWindowStyle.Minimized
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _processByBacktestId[backtestId] = process;

            process.Exited += (sender, args) =>
            {
                if (Disposed)
                {
                    // handle abort
                    return;
                }

                _processByBacktestId.TryRemove(backtestId, out process);
                var backtestResult = $"{backtestId}.json";
                var resultJson = Path.Combine(_rootResultDirectory, backtestId, backtestResult);
                NewResult(File.Exists(resultJson) ? File.ReadAllText(resultJson) : null, backtestId);
                process.DisposeSafely();
            };

            process.Start();

            return backtestId;
        }

        /// <summary>
        /// Stops lean process
        /// </summary>
        /// <param name="backtestId">Specified backtest id</param>
        protected override void AbortLean(string backtestId)
        {
            Process process;
            if (_processByBacktestId.TryRemove(backtestId, out process))
            {
                process.Kill();
                process.DisposeSafely();
            }
        }

        protected override void SendUpdate()
        {
            var estimate = GetCurrentEstimate();
            Log.Trace($"ConsoleLeanOptimizer.SendUpdate(): {estimate}");
        }
    }
}
