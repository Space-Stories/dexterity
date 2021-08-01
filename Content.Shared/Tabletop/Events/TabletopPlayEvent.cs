﻿using System;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Tabletop.Events
{
    /**
     * <summary>
     * An event sent by the server to the client to tell the client to open a tabletop game window.
     * </summary>
     */
    [Serializable, NetSerializable]
    public class TabletopPlayEvent : EntityEventArgs
    {
        public EntityUid CameraUid;
        public string Title;
        public Vector2i Size;

        public TabletopPlayEvent(EntityUid cameraUid, string title, Vector2i size)
        {
            CameraUid = cameraUid;
            Title = title;
            Size = size;
        }
    }
}
