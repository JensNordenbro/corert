// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.IL;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Interop
{
    public static class MarshalHelpers
    {
        /// <summary>
        /// Returns true if this is a type that doesn't require marshalling.
        /// </summary>
        public static bool IsBlittableType(TypeDesc type)
        {
            type = type.UnderlyingType;

            if (type.IsValueType)
            {
                if (type.IsPrimitive)
                {
                    // All primitive types except char and bool are blittable
                    TypeFlags category = type.Category;
                    if (category == TypeFlags.Boolean || category == TypeFlags.Char)
                        return false;

                    return true;
                }

                foreach (FieldDesc field in type.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    TypeDesc fieldType = field.FieldType;

                    // TODO: we should also reject fields that specify custom marshalling
                    if (!MarshalHelpers.IsBlittableType(fieldType))
                    {
                        // This field can still be blittable if it's a Char and marshals as Unicode
                        var owningType = field.OwningType as MetadataType;
                        if (owningType == null)
                            return false;

                        if (fieldType.Category != TypeFlags.Char ||
                            owningType.PInvokeStringFormat == PInvokeStringFormat.AnsiClass)
                            return false;
                    }
                }
                return true;
            }

            if (type.IsPointer || type.IsFunctionPointer)
                return true;

            return false;
        }


        /// <summary>
        /// Returns true if the PInvoke target should be resolved lazily.
        /// </summary>
        public static bool UseLazyResolution(MethodDesc method, string importModule, PInvokeILEmitterConfiguration configuration)
        {
            bool? forceLazyResolution = configuration.ForceLazyResolution;
            if (forceLazyResolution.HasValue)
                return forceLazyResolution.Value;

            // In multi-module library mode, the WinRT p/invokes in System.Private.Interop cause linker failures
            // since we don't link against the OS libraries containing those APIs. Force them to be lazy.
            // See https://github.com/dotnet/corert/issues/2601
            string assemblySimpleName = ((IAssemblyDesc)((MetadataType)method.OwningType).Module).GetName().Name;
            if (assemblySimpleName == "System.Private.Interop")
            {
                return true;
            }

            // Determine whether this call should be made through a lazy resolution or a static reference
            // Eventually, this should be controlled by a custom attribute (or an extension to the metadata format).
            if (importModule == "[MRT]" || importModule == "*")
                return false;

            if (method.Context.Target.IsWindows)
            {
                return !importModule.StartsWith("api-ms-win-");
            }
            else 
            {
                // Account for System.Private.CoreLib.Native / System.Globalization.Native / System.Native / etc
                return !importModule.StartsWith("System.");
            }
        }
    }
}