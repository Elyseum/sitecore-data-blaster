﻿using System;
using System.Collections.Generic;
using System.IO;
using Rainbow.Model;
using Sitecore;
using Sitecore.DataBlaster.Load;
using Convert = System.Convert;

namespace Unicorn.DataBlaster.Sync
{
	/// <summary>
	/// Maps a Unicorn item to a bulk load item.
	/// </summary>
	public class ItemMapper
	{
		private static readonly HashSet<Guid> BlobFields = new HashSet<Guid>(
			new[]
			{
				Guid.Parse("{FF8A2D01-8A77-4F1B-A966-65806993CD31}"),
				Guid.Parse("{40E50ED9-BA07-4702-992E-A912738D32DC}"),
				Guid.Parse("{DBBE7D99-1388-4357-BB34-AD71EDF18ED3}")
			});

		public virtual BulkLoadItem ToBulkLoadItem(IItemData itemData, BulkLoadContext context, BulkLoadAction loadAction)
		{
			if (itemData == null) throw new ArgumentNullException(nameof(itemData));
			if (context == null) throw new ArgumentNullException(nameof(context));

			var bulkItem = new BulkLoadItem(
				loadAction,
				itemData.Id,
				itemData.TemplateId,
				itemData.BranchId,
				itemData.ParentId,
				itemData.Path,
				sourceInfo: $"IItemData.dbName={itemData.DatabaseName}");

			foreach (var sharedField in itemData.SharedFields)
			{
				AddSyncField(context, bulkItem, sharedField);
			}

			foreach (var languagedFields in itemData.UnversionedFields)
			{
				foreach (var field in languagedFields.Fields)
				{
					AddSyncField(context, bulkItem, field, languagedFields.Language.Name);
				}
			}

			foreach (var versionFields in itemData.Versions)
			{
				foreach (var field in versionFields.Fields)
				{
					AddSyncField(context, bulkItem, field, versionFields.Language.Name, versionFields.VersionNumber);
				}

				AddStatisticsFieldsWhenMissing(bulkItem, versionFields.Language.Name, versionFields.VersionNumber);
			}

			// Serialized items don't contain the original blob id.
			context.LookupBlobIds = true;

			return bulkItem;
		}

		protected virtual void AddSyncField(BulkLoadContext context, BulkLoadItem bulkItem, IItemFieldValue itemField, string language = null, int versionNumber = 1)
		{
			var fieldId = itemField.FieldId;
			var isBlob = BlobFields.Contains(fieldId);
			var fieldValue = itemField.Value;
			var fieldName = itemField.NameHint;

			Func<Stream> blob = null;
			if (isBlob)
			{
				byte[] blobBytes;
				try
				{
					blobBytes = Convert.FromBase64String(fieldValue);
				}
				catch (Exception ex)
				{
					blobBytes = new byte[] { };
					context.Log.Error(
						$"Unable to read blob from field '{fieldId}' in item with id '{bulkItem.Id}', " +
						$"item path '{bulkItem.ParentId}' and source info '{bulkItem.SourceInfo}', defaulting to empty value.", ex);
				}
				blob = () => new MemoryStream(blobBytes);

				// Field value should contain blob id, but we don't know the blob id for serialized items.
				// Leave empty and let 'SplitTempTable' sql script find existing blob ids.
				fieldValue = null;
			}

			if (language == null)
			{
				bulkItem.AddSharedField(fieldId, fieldValue, blob, isBlob, fieldName);
			}
			else
			{
				bulkItem.AddVersionedField(fieldId, language, versionNumber, fieldValue, blob, isBlob, fieldName);
			}
		}

		protected virtual void AddStatisticsFieldsWhenMissing(BulkLoadItem bulkItem, string language, int versionNumber = 1)
		{
			var user = Sitecore.Context.User.Name;

			if (bulkItem.GetField(FieldIDs.Created.Guid, language, versionNumber) == null)
				bulkItem.AddVersionedField(FieldIDs.Created.Guid, language, versionNumber, DateUtil.IsoNow, name: "__Created");

			if (bulkItem.GetField(FieldIDs.CreatedBy.Guid, language, versionNumber) == null)
				bulkItem.AddVersionedField(FieldIDs.CreatedBy.Guid, language, versionNumber, user, name: "__Created by");

			if (bulkItem.LoadAction == BulkLoadAction.Update || bulkItem.LoadAction == BulkLoadAction.UpdateExistingItem)
			{
				if (bulkItem.GetField(FieldIDs.UpdatedBy.Guid, language, versionNumber) == null)
					bulkItem.AddVersionedField(FieldIDs.UpdatedBy.Guid, language, versionNumber, user, name: "__Updated");

				if (bulkItem.GetField(FieldIDs.Updated.Guid, language, versionNumber) == null)
					bulkItem.AddVersionedField(FieldIDs.Updated.Guid, language, versionNumber, DateUtil.IsoNowWithTicks, name: "__Updated by");
			}
		}
	}
}