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

using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Packets;

namespace QuantConnect.Optimizer
{
    public class OptimizationNodePacket : Packet
    {
        /// <summary>
        /// User Id placing request
        /// </summary>
        [JsonProperty(PropertyName = "iUserID")]
        public int UserId = 0;
        /// User API Token
        [JsonProperty(PropertyName = "sUserToken")]
        public string UserToken = "";
        /// <summary>
        /// Project Id of the request
        /// </summary>
        [JsonProperty(PropertyName = "iProjectID")]
        public int ProjectId = 0;

        public string OptimizationId = "";

        public string OptimizationStrategy = "";

        public string OptimizationManager = "";

        public Dictionary<string, string> OptimizationSettings;

        public Dictionary<string, string> Criterion;

        public Dictionary<string, OptimizationParameter> OptimizationParameters;

        public OptimizationNodePacket() : base(PacketType.OptimizationNode)
        {

        }
    }
}
