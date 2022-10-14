using System;
using UnityEngine;

namespace Cyan.CT
{
    // TODO change to abstract class to properly support cloning?
    public interface ICyanTriggerCustomType
    {
        Type GetBaseType();
        string GetTypeDisplayName();
        ICyanTriggerCustomType Clone();
        GUIContent GetDocumentationContent();
        Action GetDocumentationAction();
    }
}