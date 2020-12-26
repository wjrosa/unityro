﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ItemManager : MonoBehaviour {

    private void Awake() {
        Core.NetworkClient.HookPacket(ZC.ITEM_FALL_ENTRY5.HEADER, OnItemSpamInGround);
        Core.NetworkClient.HookPacket(ZC.ITEM_PICKUP_ACK7.HEADER, OnItemPickup);
        Core.NetworkClient.HookPacket(ZC.ITEM_DISAPPEAR.HEADER, OnItemDisappear);
        Core.NetworkClient.HookPacket(ZC.INVENTORY_ITEMLIST_EQUIP.HEADER, OnInventoryUpdate);
    }

    private void OnInventoryUpdate(ushort cmd, int size, InPacket packet) {
        var list = new List<ItemInfo>();
        if (packet is ZC.INVENTORY_ITEMLIST_EQUIP) {
            var pkt = packet as ZC.INVENTORY_ITEMLIST_EQUIP;
            list = pkt.Inventory;
        }

        if (list.IsEmpty()) return;

        // TODO apply a diff here
        // TODO find out how favorite tab works
        List<Item> inventory = list.Select(itemInfo => {
            var item = DBManager.GetItemInfo(itemInfo.ItemID);
            if (item == null) return null;
            var texture = FileManager.Load(DBManager.GetItemResPath(item, itemInfo.IsIdentified)) as Texture2D; 

            item.info = itemInfo;
            item.texture = texture;
            
            switch((ItemType)item.info.itemType) {
                case ItemType.HEALING:
                case ItemType.USABLE:
                case ItemType.USABLE_SKILL:
                case ItemType.USABLE_UNK:
                    item.tab = InventoryType.ITEM;
                    break;

                case ItemType.WEAPON:
                case ItemType.EQUIP:
                case ItemType.PETEGG:
                case ItemType.PETEQUIP:
                    item.tab = InventoryType.EQUIP;
                    break;

                default:
                case ItemType.ETC:
                case ItemType.CARD:
                case ItemType.AMMO:
                    item.tab = InventoryType.ETC;
                    break;
            }

            return item;
        }).Where(it => it != null).ToList();

        Core.Session.Entity.SetInventory(inventory);
        MapController.Instance.UIController.UpdateEquipment();
    }

    private void OnItemDisappear(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ITEM_DISAPPEAR) {
            var pkt = packet as ZC.ITEM_DISAPPEAR;

            Core.EntityManager.RemoveEntity(pkt.GID);
        }
    }

    private void OnItemPickup(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ITEM_PICKUP_ACK7) {
            var pkt = packet as ZC.ITEM_PICKUP_ACK7;

            if (pkt.result != 0) {
                Debug.Log("Failed to pick item");
            }

            DisplayPopup(pkt);
            // TODO add to inventory
        }
    }

    private void DisplayPopup(ZC.ITEM_PICKUP_ACK7 pkt) {
        Item item = DBManager.GetItemInfo(pkt.id);
        string itemPath = DBManager.GetItemPath(pkt.id, pkt.IsIdentified);
        SPR spr = FileManager.Load(itemPath + ".spr") as SPR;
        if (spr == null) return;
        var label = $"{(pkt.IsIdentified ? item.identifiedDisplayName : item.unidentifiedDisplayName)} - {pkt.count} obtained";
        MapController.Instance.UIController.DisplayPopup(spr.GetSprites()[0], label);
    }

    private void OnItemSpamInGround(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ITEM_FALL_ENTRY5) {
            var pkt = packet as ZC.ITEM_FALL_ENTRY5;

            var x = pkt.x - 0.5 + pkt.subX / 12;
            var z = pkt.y - 0.5 + pkt.subY / 12;
            var y = Core.PathFinding.GetCellHeight((int)x, (int)z) + 5.0;

            Core.EntityManager.SpawnItem(new ItemSpawnInfo() {
                GID = pkt.id,
                mapID = pkt.mapID,
                Position = new Vector3((float)x, (float)y, (float)z),
                amount = pkt.amount,
                IsIdentified = pkt.identified == 1,
                dropEffectMode = pkt.dropEffectMode,
                showDropEffect = pkt.showDropEffect
            });
        }
    }
}
