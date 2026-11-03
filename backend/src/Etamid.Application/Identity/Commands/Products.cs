using System.Globalization;
using ClosedXML.Excel;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Files;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record ProductDto(
    long Id, string Name, string? Description, decimal Price, int Quantity, string? ImageUrl, DateTime CreatedAt,
    string? ImageFileId = null);

internal static class ProductMapper
{
    public static async Task<ProductDto> ToDtoAsync(
        IApplicationDbContext db, IFileUrlResolver urls, Product p, CancellationToken ct)
    {
        string? image = null;
        string? imageFileId = null;
        if (p.ImageFileId is { } fileId)
        {
            var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, ct);
            image = urls.Resolve(file);
            imageFileId = file?.PublicId.ToString();
        }
        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Quantity, image, p.CreatedAt, imageFileId);
    }

    public static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public static async Task<IReadOnlyList<ProductDto>> ToDtoListAsync(
        IApplicationDbContext db, IFileUrlResolver urls, IReadOnlyList<Product> products, CancellationToken ct)
    {
        var fileIds = products.Where(p => p.ImageFileId is not null).Select(p => p.ImageFileId!.Value).Distinct().ToList();
        var files = await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);
        return products.Select(p => new ProductDto(
            p.Id, p.Name, p.Description, p.Price, p.Quantity,
            p.ImageFileId is { } fileId ? urls.Resolve(files.GetValueOrDefault(fileId)) : null,
            p.CreatedAt,
            p.ImageFileId is { } fileId2 ? files.GetValueOrDefault(fileId2)?.PublicId.ToString() : null)).ToList();
    }
}

// ---------- GET /sellers/{id}/products and GET /me/products

public sealed record GetSellerProductsQuery(long SellerId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<ProductDto>>>;

public sealed class GetSellerProductsHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<GetSellerProductsQuery, Result<PagedResult<ProductDto>>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(GetSellerProductsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } _) return CommonErrors.Unauthorized;
        if (!await db.Users.AnyAsync(u => u.Id == r.SellerId, ct)) return CommonErrors.NotFound;
        return await ProductQueries.PageAsync(db, urls, r.SellerId, r.Page, r.PageSize, ct);
    }
}

public sealed record ListMyProductsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<ProductDto>>>;

public sealed class ListMyProductsHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<ListMyProductsQuery, Result<PagedResult<ProductDto>>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(ListMyProductsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        return await ProductQueries.PageAsync(db, urls, userId, r.Page, r.PageSize, ct);
    }
}

internal static class ProductQueries
{
    public static async Task<Result<PagedResult<ProductDto>>> PageAsync(
        IApplicationDbContext db, IFileUrlResolver urls, long sellerId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.Products.AsNoTracking().Where(p => p.SellerId == sellerId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<ProductDto>(await ProductMapper.ToDtoListAsync(db, urls, items, ct), page, pageSize, total);
    }
}

// ---------- POST /me/products and PUT /me/products/{id}

public sealed record AddProductCommand(
    string Name, string? Description, decimal Price, int Quantity, string? ImagePublicId)
    : IRequest<Result<ProductDto>>;

public sealed record UpdateProductCommand(
    long Id, string Name, string? Description, decimal Price, int Quantity, string? ImagePublicId)
    : IRequest<Result<ProductDto>>;

public sealed class AddProductValidator : AbstractValidator<AddProductCommand>
{
    public AddProductValidator()
    {
        RuleFor(x => x.Name).MustBeRequired(150);
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithErrorCode("TOO_LONG")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Price)
            .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("INVALID_VALUE")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.ImagePublicId!)
            .Must(v => Guid.TryParse(v, out _)).WithErrorCode("INVALID_VALUE")
            .When(x => !string.IsNullOrWhiteSpace(x.ImagePublicId));
    }
}

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(x => x.Name).MustBeRequired(150);
        RuleFor(x => x.Description)
            .MaximumLength(1000).WithErrorCode("TOO_LONG")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Price)
            .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("INVALID_VALUE")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.ImagePublicId!)
            .Must(v => Guid.TryParse(v, out _)).WithErrorCode("INVALID_VALUE")
            .When(x => !string.IsNullOrWhiteSpace(x.ImagePublicId));
    }
}

public sealed class AddProductHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<AddProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(AddProductCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var image = await ProductImage.ResolveAsync(db, userId, r.ImagePublicId, ct);
        if (image is null && !string.IsNullOrWhiteSpace(r.ImagePublicId))
            return Error.Validation("INVALID_VALUE", "imagePublicId");

        var product = new Product
        {
            SellerId = userId,
            Name = r.Name.Trim(),
            Description = ProductMapper.Clean(r.Description),
            Price = r.Price,
            Quantity = r.Quantity,
            ImageFileId = image?.Id
        };
        if (image is not null) image.Status = FileStatus.Attached;
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return await ProductMapper.ToDtoAsync(db, urls, product, ct);
    }
}

public sealed class UpdateProductHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == r.Id && p.SellerId == userId, ct);
        if (product is null) return CommonErrors.NotFound;

        var image = await ProductImage.ResolveAsync(db, userId, r.ImagePublicId, ct);
        if (image is null && !string.IsNullOrWhiteSpace(r.ImagePublicId))
            return Error.Validation("INVALID_VALUE", "imagePublicId");

        product.Name = r.Name.Trim();
        product.Description = ProductMapper.Clean(r.Description);
        product.Price = r.Price;
        product.Quantity = r.Quantity;
        product.ImageFileId = image?.Id;
        if (image is not null) image.Status = FileStatus.Attached;

        await db.SaveChangesAsync(ct);
        return await ProductMapper.ToDtoAsync(db, urls, product, ct);
    }
}

internal static class ProductImage
{
    /// <summary>Validates a product image the same way deal images are validated:
    /// seller-owned, still uploaded, and actually an image. Null input means no image.</summary>
    public static async Task<StoredFile?> ResolveAsync(
        IApplicationDbContext db, long userId, string? imagePublicId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(imagePublicId)) return null;
        var publicId = Guid.Parse(imagePublicId);
        var file = await db.Files.FirstOrDefaultAsync(
            f => f.PublicId == publicId && f.OwnerUserId == userId && f.Status != FileStatus.Deleted, ct);
        if (file is null || !file.ContentType.StartsWith("image/", StringComparison.Ordinal)) return null;
        return file;
    }
}

// ---------- DELETE /me/products/{id}

public sealed record DeleteProductCommand(long Id) : IRequest<Result>;

public sealed class DeleteProductHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public async Task<Result> Handle(DeleteProductCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == r.Id && p.SellerId == userId, ct);
        if (product is null) return CommonErrors.NotFound;
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- POST /me/products/import

/// <summary>One bad row in an import. Row is the Excel row number (header is row 1),
/// so the seller can find it in the same file they uploaded.</summary>
public sealed record ImportRowError(int Row, string Field, string Code);

public sealed record ImportProductsDto(int Imported, IReadOnlyList<ImportRowError> Errors);

public sealed record ImportProductsCommand(Stream Content, string? FileName) : IRequest<Result<ImportProductsDto>>;

public sealed class ImportProductsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ImportProductsCommand, Result<ImportProductsDto>>
{
    public const int MaxRows = 500;

    public async Task<Result<ImportProductsDto>> Handle(ImportProductsCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await r.Content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > FileUploadService.MaxBytes) return FileErrors.TooLarge;
        }
        if (buffer.Length == 0) return FileErrors.Required;

        XLWorkbook workbook;
        try
        {
            buffer.Position = 0;
            workbook = new XLWorkbook(buffer);
        }
        catch
        {
            return Error.Validation("PRODUCT_IMPORT_BAD_FILE");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null) return Error.Validation("PRODUCT_IMPORT_BAD_FILE");

            var header = MapHeaders(sheet.Row(1));
            if (!header.ContainsKey("name") || !header.ContainsKey("price"))
                return Error.Validation("PRODUCT_IMPORT_BAD_HEADER");

            var rows = sheet.RowsUsed().Skip(1)
                .Where(row => Enumerable.Range(1, 4).Any(i => !string.IsNullOrWhiteSpace(row.Cell(i).GetString())))
                .ToList();
            if (rows.Count == 0) return Error.Validation("PRODUCT_IMPORT_EMPTY");
            if (rows.Count > MaxRows) return Error.Validation("PRODUCT_IMPORT_TOO_MANY_ROWS");

            var errors = new List<ImportRowError>();
            var products = new List<Product>();
            foreach (var row in rows)
            {
                var rowNumber = row.RowNumber();
                var name = CellText(row, header, "name");
                var description = CellText(row, header, "description");
                var priceText = CellText(row, header, "price");
                var quantityText = CellText(row, header, "quantity");

                if (string.IsNullOrWhiteSpace(name)) errors.Add(new ImportRowError(rowNumber, "name", "REQUIRED"));
                else if (name!.Length > 150) errors.Add(new ImportRowError(rowNumber, "name", "TOO_LONG"));
                if (description is not null && description.Length > 1000)
                    errors.Add(new ImportRowError(rowNumber, "description", "TOO_LONG"));

                var priceOk = decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price);
                if (string.IsNullOrWhiteSpace(priceText)) errors.Add(new ImportRowError(rowNumber, "price", "REQUIRED"));
                else if (!priceOk || price <= 0m || price > 10_000_000m || decimal.Round(price, 2) != price)
                    errors.Add(new ImportRowError(rowNumber, "price", "INVALID_VALUE"));

                var quantityOk = int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity);
                if (string.IsNullOrWhiteSpace(quantityText)) errors.Add(new ImportRowError(rowNumber, "quantity", "REQUIRED"));
                else if (!quantityOk || quantity < 0) errors.Add(new ImportRowError(rowNumber, "quantity", "INVALID_VALUE"));

                if (errors.Any(e => e.Row == rowNumber)) continue;
                products.Add(new Product
                {
                    SellerId = userId,
                    Name = name!.Trim(),
                    Description = ProductMapper.Clean(description),
                    Price = price,
                    Quantity = quantity
                });
            }

            // All-or-nothing: any bad row rejects the whole file, nothing is saved.
            if (errors.Count > 0)
                return Result.Failure<ImportProductsDto>(new Error(
                    "PRODUCT_IMPORT_FAILED", ErrorType.Validation, null,
                    new Dictionary<string, object?> { ["errors"] = errors }));

            db.Products.AddRange(products);
            await db.SaveChangesAsync(ct);
            return new ImportProductsDto(products.Count, []);
        }
    }

    private static Dictionary<string, int> MapHeaders(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = cell.GetString().Trim().ToLowerInvariant() switch
            {
                "الاسم" or "name" => "name",
                "الوصف" or "description" => "description",
                "السعر" or "السعر (ر.س)" or "price" => "price",
                "الكمية" or "المخزون" or "quantity" or "stock" => "quantity",
                _ => null
            };
            if (key is not null && !map.ContainsKey(key)) map[key] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static string? CellText(IXLRow row, Dictionary<string, int> header, string field)
    {
        if (!header.TryGetValue(field, out var column)) return null;
        var text = row.Cell(column).GetString().Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

/// <summary>Builds the import template: a "المنتجات" sheet with only the headers,
/// plus a separate "مثال" sheet showing one filled row, so the example itself
/// can never be imported by accident.</summary>
public static class ProductTemplate
{
    private static readonly string[] Headers = ["الاسم", "الوصف", "السعر", "الكمية"];

    public static byte[] Build()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("المنتجات");
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
        }
        sheet.Columns().AdjustToContents();

        var example = workbook.Worksheets.Add("مثال");
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = example.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
        }
        example.Cell(2, 1).Value = "سماعات لاسلكية";
        example.Cell(2, 2).Value = "سماعات بلوتوث بحالة ممتازة";
        example.Cell(2, 3).Value = 250;
        example.Cell(2, 4).Value = 3;
        example.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
