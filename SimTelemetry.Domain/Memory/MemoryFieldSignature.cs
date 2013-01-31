﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SimTelemetry.Domain.Memory
{
    public class MemoryFieldSignature<T> : MemoryField<T>
    {
        public IEnumerable<MemoryFieldSignaturePointer> Pointers { get; protected set; }
        public string Signature { get; protected set; }

        public bool Initialized { get; protected set; }

        public override void Refresh()
        {
            if (!Initialized)
                Scan();

            if (!Initialized)
                return;

            base.Refresh();
        }

        public virtual void Scan()
        {
            if (Memory.Scanner.Enabled == false)
                throw new Exception("Please enable SignatureScanner first");

            var result = Memory.Scanner.Scan<uint>(MemoryRegionType.EXECUTE, Signature);

            // Search the address and offset.
            switch (AddressType)
            {
                case MemoryAddress.StaticAbsolute:
                case MemoryAddress.Static:
                    if (result == 0) return;

                    if (Pointers.Count() == 0)
                    {
                        // The result is directly our address
                        Address = (int) result;
                    }
                    else
                    {
                        // We must follow one pointer.
                        var computedAddress = 0;
                        if (AddressType == MemoryAddress.Static)
                        {
                            computedAddress = Memory.BaseAddress + (int) result;
                        }
                        else
                        {
                            computedAddress = (int) result;
                        }

                        foreach(var ptr in Pointers)
                        {
                            computedAddress = Memory.Reader.ReadInt32(computedAddress + ptr.Offset);
                        }

                        Address = computedAddress;
                    }
                    break;
                case MemoryAddress.Dynamic:
                    Offset = (int) result;
                    break;
                default:
                    throw new Exception("AddressType for '" + Name + "' is not valid");
                    break;
            }

            Initialized = true;
        }

        public MemoryFieldSignature(string name, MemoryAddress type, string signature, IEnumerable<MemoryFieldSignaturePointer> pointers, int size)
            : base(name, type, 0, size)
        {
            Signature = signature;
            Pointers = pointers;
            Initialized = false;
        }
        public MemoryFieldSignature(string name, MemoryAddress type, string signature, IEnumerable<int> pointers, int size)
            : base(name, type, 0, size)
        {
            Signature = signature;
            Pointers = pointers.Select(pointer => new MemoryFieldSignaturePointer(pointer)).ToList();
            Initialized = false;
        }
    }
}
