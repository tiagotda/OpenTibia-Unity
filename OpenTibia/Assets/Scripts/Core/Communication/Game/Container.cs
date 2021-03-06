﻿using System.Collections.Generic;

namespace OpenTibiaUnity.Core.Communication.Game
{
    internal partial class ProtocolGame : Internal.Protocol
    {
        private void ParseOpenContainer(Internal.ByteArray message) {
            byte containerId = message.ReadUnsignedByte();
            var objectIcon = ReadObjectInstance(message);
            string name = message.ReadString();
            byte nOfSlotsPerPage = message.ReadUnsignedByte(); // capacity of shown view
            bool isSubContainer = message.ReadBoolean();

            bool isDragAndDropEnabled = true;
            bool isPaginationEnabled = false;
            int nOfTotalObjects = 0;
            int indexOfFirstObject = 0;
            int nOfContentObjects = 0; // objects in the current shown view //
            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameContainerPagination)) {
                isDragAndDropEnabled = message.ReadBoolean();
                isPaginationEnabled = message.ReadBoolean();
                nOfTotalObjects = message.ReadUnsignedShort();
                indexOfFirstObject = message.ReadUnsignedShort();
                nOfContentObjects = message.ReadUnsignedByte();

                if (nOfContentObjects > nOfSlotsPerPage)
                    throw new System.Exception("ProtocolGame.ParseOpenContainer: Number of content objects " + nOfContentObjects + " exceeds number of slots per page " + nOfSlotsPerPage);

                if (nOfContentObjects > nOfTotalObjects)
                    throw new System.Exception("Connection.readSCONTAINER: Number of content objects " + nOfContentObjects + " exceeds number of total objects " + nOfTotalObjects);
            } else {
                nOfContentObjects = message.ReadUnsignedByte();
                nOfTotalObjects = nOfContentObjects;

                if (nOfContentObjects > nOfSlotsPerPage)
                    throw new System.Exception("ProtocolGame.ParseOpenContainer: Number of content objects " + nOfContentObjects + " exceeds the capaciy " + nOfSlotsPerPage);
            }
            
            var containerView = ContainerStorage.CreateContainerView(containerId, objectIcon, name, isSubContainer, isDragAndDropEnabled, isPaginationEnabled, nOfSlotsPerPage, nOfTotalObjects - nOfContentObjects, indexOfFirstObject);
            
            for (int i = 0; i < nOfContentObjects; i++)
                containerView.AddObject(indexOfFirstObject + i, ReadObjectInstance(message));
        }

        private void ParseCloseContainer(Internal.ByteArray message) {
            byte containerId = message.ReadUnsignedByte();
            ContainerStorage.CloseContainerView(containerId);
        }

        private void ParseCreateInContainer(Internal.ByteArray message) {
            byte containerId = message.ReadUnsignedByte();
            ushort slot = 0;
            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameContainerPagination))
                slot = message.ReadUnsignedShort();
            var @object = ReadObjectInstance(message);

            var containerView = ContainerStorage.GetContainerView(containerId);
            if (!!containerView)
                containerView.AddObject(slot, @object);
        }

        private void ParseChangeInContainer(Internal.ByteArray message) {
            byte containerId = message.ReadUnsignedByte();
            ushort slot = 0;
            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameContainerPagination))
                slot = message.ReadUnsignedShort();
            else
                slot = message.ReadUnsignedByte();
            var @object = ReadObjectInstance(message);

            var containerView = ContainerStorage.GetContainerView(containerId);
            if (!!containerView)
                containerView.ChangeObject(slot, @object);
        }

        private void ParseDeleteInContainer(Internal.ByteArray message) {
            byte containerId = message.ReadUnsignedByte();
            ushort slot;
            Appearances.ObjectInstance appendObject = null;

            if (OpenTibiaUnity.GameManager.GetFeature(GameFeature.GameContainerPagination)) {
                slot = message.ReadUnsignedShort();
                ushort itemId = message.ReadUnsignedShort();
                
                if (itemId != 0)
                    appendObject = ReadObjectInstance(message, itemId);
            } else {
                slot = message.ReadUnsignedByte();
            }

            var containerView = ContainerStorage.GetContainerView(containerId);
            if (!!containerView)
                containerView.RemoveObject(slot, appendObject);
        }
    }
}
