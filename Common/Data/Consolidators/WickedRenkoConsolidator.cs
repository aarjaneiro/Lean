/*
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
using Python.Runtime;
using QuantConnect.Data.Market;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// This consolidator can transform a stream of <see cref="BaseData"/> instances into a stream of <see cref="RenkoBar"/>
    /// </summary>
    public class WickedRenkoConsolidator : IDataConsolidator
    {
        private readonly decimal _barSize;
        private readonly Func<IBaseData, decimal> _selector;
        private readonly Func<IBaseData, decimal> _volumeSelector;
        private DateTime _closeOn;
        private decimal _closeRate;

        private RenkoBar _currentBar;
        private DataConsolidatedHandler _dataConsolidatedHandler;

        private bool _firstTick = true;
        private decimal _highRate;
        private RenkoBar _lastWicko = null;
        private decimal _lowRate;

        private DateTime _openOn;
        private decimal _openRate;


        /// <summary>
        /// Initializes a new instance of the <see cref="WickedRenkoConsolidator"/> class using the specified <paramref name="barSize"/>.
        /// Renko consolidators can be initialized using this method.
        /// </summary>
        /// <param name="barSize">The constant value size of each bar</param>
        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator"/> class using the specified <paramref name="barSize"/>.
        /// </summary>
        /// <param name="barSize">The constant value size of each bar</param>
        protected WickedRenkoConsolidator(decimal barSize)
        {
            _barSize = barSize;
            Type = RenkoType.Wicked;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator"/> class using the specified <paramref name="barSize"/>.
        /// The value selector will by default select <see cref="IBaseData.Value"/>
        /// The volume selector will by default select zero.
        /// </summary>
        /// <param name="barSize">The constant value size of each bar</param>
        /// <param name="evenBars">When true bar open/close will be a multiple of the barSize</param>
        public WickedRenkoConsolidator(decimal barSize, bool evenBars = true)
        {
            _barSize = barSize;
            _selector = x => x.Value;
            _volumeSelector = x => 0;

            Type = RenkoType.Wicked;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator" /> class.
        /// </summary>
        /// <param name="barSize">The size of each bar in units of the value produced by <paramref name="selector"/></param>
        /// <param name="selector">Extracts the value from a data instance to be formed into a <see cref="RenkoBar"/>. The default
        /// value is (x => x.Value) the <see cref="IBaseData.Value"/> property on <see cref="IBaseData"/></param>
        /// <param name="volumeSelector">Extracts the volume from a data instance. The default value is null which does
        /// not aggregate volume per bar.</param>
        /// <param name="evenBars">When true bar open/close will be a multiple of the barSize</param>
        public WickedRenkoConsolidator(
            decimal barSize, Func<IBaseData, decimal> selector, Func<IBaseData, decimal> volumeSelector = null
            )
        {
            EpsilonCheck(barSize);

            _barSize = barSize;
            _selector = selector ?? (x => x.Value);
            _volumeSelector = volumeSelector ?? (x => 0);

            Type = RenkoType.Wicked;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator" /> class.
        /// </summary>
        /// <param name="barSize">The size of each bar in units of the value produced by <paramref name="selector"/></param>
        /// <param name="selector">Extracts the value from a data instance to be formed into a <see cref="RenkoBar"/>. The default
        /// value is (x => x.Value) the <see cref="IBaseData.Value"/> property on <see cref="IBaseData"/></param>
        /// <param name="volumeSelector">Extracts the volume from a data instance. The default value is null which does
        /// not aggregate volume per bar.</param>
        /// <param name="evenBars">When true bar open/close will be a multiple of the barSize</param>
        public WickedRenkoConsolidator(
            decimal barSize, PyObject selector, PyObject volumeSelector = null, bool evenBars = true
            )
            : this(barSize, evenBars)
        {
            EpsilonCheck(barSize);

            if (selector != null)
            {
                if (!selector.TryConvertToDelegate(out _selector))
                {
                    throw new ArgumentException(
                        "Unable to convert parameter 'selector' to delegate type Func<IBaseData, decimal>");
                }
            }
            else
            {
                _selector = (x => x.Value);
            }

            if (volumeSelector != null)
            {
                if (!volumeSelector.TryConvertToDelegate(out _volumeSelector))
                {
                    throw new ArgumentException(
                        "Unable to convert parameter 'volumeSelector' to delegate type Func<IBaseData, decimal>");
                }
            }
            else
            {
                _volumeSelector = (x => 0);
            }
        }

        /// <summary>
        /// Gets the kind of the bar
        /// </summary>
        public RenkoType Type { get; private set; }

        /// <summary>
        /// Gets the bar size used by this consolidator
        /// </summary>
        public decimal BarSize => _barSize;

        // Used for unit tests
        internal RenkoBar OpenRenkoBar =>
            new RenkoBar(null, _openOn, _closeOn, _barSize, _openRate, _highRate, _lowRate, _closeRate);

        /// <summary>
        /// Event handler that fires when a new piece of data is produced
        /// </summary>
        event DataConsolidatedHandler IDataConsolidator.DataConsolidated
        {
            add { _dataConsolidatedHandler += value; }
            remove { _dataConsolidatedHandler -= value; }
        }

        /// <summary>
        /// Gets the most recently consolidated piece of data. This will be null if this consolidator
        /// has not produced any data yet.
        /// </summary>
        public IBaseData Consolidated { get; private set; }

        /// <summary>
        /// Gets a clone of the data being currently consolidated
        /// </summary>
        public IBaseData WorkingData => _currentBar?.Clone();

        /// <summary>
        /// Gets the type consumed by this consolidator
        /// </summary>
        public Type InputType => typeof(IBaseData);

        /// <summary>
        /// Gets <see cref="RenkoBar"/> which is the type emitted in the <see cref="IDataConsolidator.DataConsolidated"/> event.
        /// </summary>
        public Type OutputType => typeof(RenkoBar);

        /// <summary>
        /// Updates this consolidator with the specified data. This method is
        /// responsible for raising the DataConsolidated event
        /// </summary>
        /// <param name="data">The new data for the consolidator</param>
        public void Update(IBaseData data) => UpdateWicked(data);

        /// <summary>
        /// Scans this consolidator to see if it should emit a bar due to time passing
        /// </summary>
        /// <param name="currentLocalTime">The current time in the local time zone (same as <see cref="BaseData.Time"/>)</param>
        public void Scan(DateTime currentLocalTime)
        {
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            DataConsolidated = null;
            _dataConsolidatedHandler = null;
        }

        /// <summary>
        /// Event handler that fires when a new piece of data is produced
        /// </summary>
        public event EventHandler<RenkoBar> DataConsolidated;

        /// <summary>
        /// Checks a given bar to ensure it meets a minimum size requirement.
        /// </summary>
        /// <param name="barSize">The size of a bar to be checked</param>
        /// <exception cref="ArgumentOutOfRangeException">The error thrown if the minimum size is not surpassed</exception>
        private static void EpsilonCheck(decimal barSize)
        {
            if (barSize < Extensions.GetDecimalEpsilon())
            {
                throw new ArgumentOutOfRangeException(nameof(barSize),
                    "WickedRenkoConsolidator bar size must be positve and greater than 1e-28");
            }
        }

        private void Rising(IBaseData data)
        {
            decimal limit;

            while (_closeRate > (limit = _openRate + _barSize))
            {
                var wicko = new RenkoBar(data.Symbol, _openOn, _closeOn, _barSize, _openRate, limit, _lowRate, limit);

                _lastWicko = wicko;

                OnDataConsolidated(wicko);

                _openOn = _closeOn;
                _openRate = limit;
                _lowRate = limit;
            }
        }

        private void Falling(IBaseData data)
        {
            decimal limit;

            while (_closeRate < (limit = _openRate - _barSize))
            {
                var wicko = new RenkoBar(data.Symbol, _openOn, _closeOn, _barSize, _openRate, _highRate, limit, limit);

                _lastWicko = wicko;

                OnDataConsolidated(wicko);

                _openOn = _closeOn;
                _openRate = limit;
                _highRate = limit;
            }
        }

        private void UpdateWicked(IBaseData data)
        {
            var rate = data.Price;

            if (_firstTick)
            {
                _firstTick = false;

                _openOn = data.Time;
                _closeOn = data.Time;
                _openRate = rate;
                _highRate = rate;
                _lowRate = rate;
                _closeRate = rate;
            }
            else
            {
                _closeOn = data.Time;

                if (rate > _highRate) _highRate = rate;

                if (rate < _lowRate) _lowRate = rate;

                _closeRate = rate;

                if (_closeRate > _openRate)
                {
                    if (_lastWicko == null || _lastWicko.Direction == BarDirection.Rising)
                    {
                        Rising(data);
                        return;
                    }

                    var limit = _lastWicko.Open + _barSize;

                    if (_closeRate > limit)
                    {
                        var wicko = new RenkoBar(data.Symbol, _openOn, _closeOn, _barSize, _lastWicko.Open, limit,
                            _lowRate, limit);

                        _lastWicko = wicko;

                        OnDataConsolidated(wicko);

                        _openOn = _closeOn;
                        _openRate = limit;
                        _lowRate = limit;

                        Rising(data);
                    }
                }
                else if (_closeRate < _openRate)
                {
                    if (_lastWicko == null || _lastWicko.Direction == BarDirection.Falling)
                    {
                        Falling(data);
                        return;
                    }

                    var limit = _lastWicko.Open - _barSize;

                    if (_closeRate < limit)
                    {
                        var wicko = new RenkoBar(data.Symbol, _openOn, _closeOn, _barSize, _lastWicko.Open, _highRate,
                            limit, limit);

                        _lastWicko = wicko;

                        OnDataConsolidated(wicko);

                        _openOn = _closeOn;
                        _openRate = limit;
                        _highRate = limit;

                        Falling(data);
                    }
                }
            }
        }


        /// <summary>
        /// Event invocator for the DataConsolidated event. This should be invoked
        /// by derived classes when they have consolidated a new piece of data.
        /// </summary>
        /// <param name="consolidated">The newly consolidated data</param>
        protected virtual void OnDataConsolidated(RenkoBar consolidated)
        {
            DataConsolidated?.Invoke(this, consolidated);

            _dataConsolidatedHandler?.Invoke(this, consolidated);

            Consolidated = consolidated;
        }
    }

    /// <summary>
    /// Provides a type safe wrapper on the RenkoConsolidator class. This just allows us to define our selector functions with the real type they'll be receiving
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    public class WickedRenkoConsolidator<TInput> : RenkoConsolidator
        where TInput : IBaseData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator" /> class.
        /// </summary>
        /// <param name="barSize">The size of each bar in units of the value produced by <paramref name="selector"/></param>
        /// <param name="selector">Extracts the value from a data instance to be formed into a <see cref="RenkoBar"/>. The default
        /// value is (x => x.Value) the <see cref="IBaseData.Value"/> property on <see cref="IBaseData"/></param>
        /// <param name="volumeSelector">Extracts the volume from a data instance. The default value is null which does
        /// not aggregate volume per bar.</param>
        /// <param name="evenBars">When true bar open/close will be a multiple of the barSize</param>
        public WickedRenkoConsolidator(
            decimal barSize, Func<TInput, decimal> selector, Func<TInput, decimal> volumeSelector = null,
            bool evenBars = true
            )
            : base(barSize, x => selector((TInput) x),
                volumeSelector == null ? (Func<IBaseData, decimal>) null : x => volumeSelector((TInput) x), evenBars)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator"/> class using the specified <paramref name="barSize"/>.
        /// The value selector will by default select <see cref="IBaseData.Value"/>
        /// The volume selector will by default select zero.
        /// </summary>
        /// <param name="barSize">The constant value size of each bar</param>
        /// <param name="evenBars">When true bar open/close will be a multiple of the barSize</param>
        public WickedRenkoConsolidator(decimal barSize, bool evenBars = true)
            : base(barSize, evenBars)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RenkoConsolidator"/> class using the specified <paramref name="barSize"/>.
        /// The value selector will by default select <see cref="IBaseData.Value"/>
        /// The volume selector will by default select zero.
        /// </summary>
        /// <param name="barSize">The constant value size of each bar</param>
        /// <param name="type">The RenkoType of the bar</param>
        public WickedRenkoConsolidator(decimal barSize)
            : base(barSize)
        {
        }

        /// <summary>
        /// Updates this consolidator with the specified data.
        /// </summary>
        /// <remarks>
        /// Type safe shim method.
        /// </remarks>
        /// <param name="data">The new data for the consolidator</param>
        public void Update(TInput data)
        {
            base.Update(data);
        }
    }
}
