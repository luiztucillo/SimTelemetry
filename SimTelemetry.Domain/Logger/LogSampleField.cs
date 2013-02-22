﻿using System;
using System.Diagnostics;
using SimTelemetry.Domain.Common;
using SimTelemetry.Domain.Memory;

namespace SimTelemetry.Domain.Logger
{
    public class LogSampleField<T> : IDataField, ILogSampleField
    {
        public string Name { get; protected set; }
        public Type ValueType { get; protected set; }
        private LogSampleGroup Group { get; set; }

        public LogSampleField(string name, Type valueType, LogSampleGroup @group)
        {
            Name = name;
            ValueType = valueType;
            Group = group;
        }


        internal int CurrentOffset { get; set; }
        public void SetOffset(int offset)
        {
            CurrentOffset = offset;
        }

        public TOut ReadAs<TOut>()
        {
            return MemoryDataConverter.Unrawify<T, TOut>(Group.Buffer, CurrentOffset);
        }

        public bool HasChanged()
        {
            return true;
        }
    }
}