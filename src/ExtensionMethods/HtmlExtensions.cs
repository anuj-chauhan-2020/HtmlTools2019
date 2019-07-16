﻿using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.WebTools.Languages.Html.Artifacts;
using Microsoft.WebTools.Languages.Html.Tree.Nodes;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace HtmlTools
{
    public static class HtmlExtensions
    {
        public static bool CompareCurrent(this TabAwareCharacterStream stream, string text, bool ignoreCase = false)
        {
            return stream.CompareTo(stream.Position, text, ignoreCase);
        }

        ///<summary>Indicates whether a stream is current at the last valid character.</summary>
        ///<remarks>IsEndOfStream() returns true if the stream is _after_ the last character, at '\0'.</remarks>
        public static bool IsAtLastCharacter(this TabAwareCharacterStream stream)
        {
            return stream.DistanceFromEnd <= 1;
        }

        public static string GetText(this IArtifact artifact, ITextSnapshot snapshot)
        {
            return snapshot.GetText(artifact.InnerRange.Start, artifact.InnerRange.Length);
        }

        public static Microsoft.VisualStudio.OLE.Interop.IServiceProvider GetServiceProvider(this IVsTextBuffer buffer)
        {
            IntPtr pUnk = IntPtr.Zero;
            try
            {
                var objectWithSite = buffer as IObjectWithSite;

                Guid gUID = typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider).GUID;
                objectWithSite.GetSite(ref gUID, out pUnk);
                return Marshal.GetObjectForIUnknown(pUnk) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
            }
            finally
            {
                if (pUnk != IntPtr.Zero)
                {
                    Marshal.Release(pUnk);
                }
            }
        }

        public static bool HasClass(this ElementNode element, string className)
        {
            AttributeNode attr = element.GetAttribute("class", true);
            if (attr == null)
            {
                return false;
            }

            string[] names = attr.Value.Split(' ');
            return names.Contains(className);
        }

        public static bool HasAttribute(this ElementNode element, string attributeName, bool ignoreCase = true)
        {
            return element.GetAttribute(attributeName, ignoreCase) != null;
        }
    }
}
