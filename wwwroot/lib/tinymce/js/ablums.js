var ListItemsAlbums = [];

function AddItemAlbums() {
    var max = parseInt($("#maxfile").val()) + 1;

    var img = "/images/no-image.png";
    var row = `<tr id="row_` + max + `">`;
    row =
        row +
        "<td><a onclick=\"ShowManagerFile('FileName_" +
        max +
        '\',2)" type="button" class="btn btn-primary">Chọn File</a></td>';
    row =
        row +
        "<td>" +
        "<img height='64px' width='64px' class='FileName_" +
        max +
        "_Preview' id='FileName_" +
        max +
        "_Preview' src='" +
        img +
        "' />" +
        ' <input type="hidden" id="FileName_' +
        max +
        '" name="ListItemsAlbums[' +
        max +
        '].Images" value="' +
        img +
        '"/></td>';
    row =
        row +
        `<td><input onchange="UpdateDataItemAlbums(` +
        max +
        `,1,event)"  name="ListItemsAlbums[` +
        max +
        `].Title" class="form-control" placeholder="Tiêu Đề" value="" /></td>`;
    row =
        row +
        '<td><input type="number" onchange="UpdateDataItemAlbums(' +
        max +
        ',2,event)" name="ListItemsAlbums[' +
        max +
        '].Ordering"  class="form-control"  value="' +
        max +
        '"/></td>';
    row =
        row +
        '<td><span class="btn btn-danger" onclick="DeleteItemAlbums(' +
        max +
        ')">Xóa</span> <input type="hidden" name="ListItemsAlbums[' +
        max +
        '].Status" value="true"/> <input type="hidden" name="ListItemsAlbums[' +
        max +
        '].Id" value="0"/>';
    row =
        row +
        `<input type="hidden" name="ListItemsAlbums[` +
        max +
        `].Deleted" id='ListItemsAlbums_` +
        max +
        `_Deleted'  value="false"/>`;
    row = row + "</td>";
    row = row + "</tr>";

    $("#tbAlbums tbody").append(row);
    $("#maxfile").val(max);
}
function DeleteItemAlbums(i) {
    console.log("DeleteItemAlbums: " + i);
    // Ẩn dòng hiện tại bằng cách sử dụng ID của <tr>
    var row = document.getElementById("row_" + i);
    if (row) {
        row.style.display = "none";

        // Clear ảnh preview
        var imgPreview = document.getElementById("FileName_" + i + "_Preview");
        if (imgPreview) {
            imgPreview.src = "/images/no-image.png";
        }

        // Clear giá trị hidden input Images
        var imgInput = document.getElementById("FileName_" + i);
        if (imgInput) {
            imgInput.value = "";
        }

        // Đánh dấu item bị xóa
        var deletedInput = document.getElementById(
            "ListItemsAlbums_" + i + "_Deleted"
        );
        if (deletedInput) {
            deletedInput.value = "true";
        } else {
            // Nếu không tìm thấy bằng ID, tìm bằng name attribute
            var deletedInputByName = document.querySelector(
                "input[name='ListItemsAlbums[" + i + "].Deleted']"
            );
            if (deletedInputByName) {
                deletedInputByName.value = "true";
            }
        }
    }
}

function AddItemsAlbums() {
    // Add 5 rows at once by calling AddItemAlbums() in a loop
    for (let i = 0; i < 5; i++) {
        AddItemAlbums();
    }
}
