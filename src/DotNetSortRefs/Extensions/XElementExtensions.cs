﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DotnetSortAndSyncRefs.Extensions
{
    internal static class XElementExtensions
    {

        public static List<XElement> GetReferenceElements(this List<XElement> elementsOfProjectFiles)
        {
            var attributesOfProjectFiles = new List<XElement>();

            foreach (var elementsOfProjectFile in elementsOfProjectFiles)
            {
                XElement node = null;

                do
                {
                    if (node == null)
                    {
                        node = elementsOfProjectFile.FirstNode as XElement;
                    }
                    else
                    {
                        node = node.NextNode as XElement;
                    }
                    attributesOfProjectFiles.Add(node);

                } while (node?.NextNode != null);

            }
            return attributesOfProjectFiles;
        }
    }
}
