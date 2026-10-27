var TinyDataListItem = {
    ListBreadcrumb: [],
    ListFolders: [],
    ListFiles: [],
    Pagination: { CurrentPage: 1, ItemsPerPage: 60, TotalItems: 0, TotalPages: 1 }
};
var TinyItemsPerPage = 60;

function TinyRenderPagination(pagination) {
    if (!pagination || pagination.TotalItems <= pagination.ItemsPerPage) {
        $("#tiny-pagination").html("");
        return;
    }

    var prevDisabled = pagination.HasPreviousPage ? "" : "disabled";
    var nextDisabled = pagination.HasNextPage ? "" : "disabled";
    var html = "<div class='d-flex justify-content-between align-items-center mt-2'>" +
        "<small>Trang " + pagination.CurrentPage + "/" + pagination.TotalPages + " - " + pagination.TotalItems + " file</small>" +
        "<div>" +
        "<button type='button' class='btn btn-sm btn-light tiny-page-btn mr-1' data-page='" + (pagination.CurrentPage - 1) + "' " + prevDisabled + ">&laquo; Trước</button>" +
        "<button type='button' class='btn btn-sm btn-light tiny-page-btn' data-page='" + (pagination.CurrentPage + 1) + "' " + nextDisabled + ">Sau &raquo;</button>" +
        "</div>" +
        "</div>";
    $("#tiny-pagination").html(html);
}

$(document).on("click", "#tiny-pagination .tiny-page-btn", function () {
    var page = parseInt($(this).attr("data-page"), 10);
    if (!page || page < 1) {
        return;
    }
    TinyGetListItems($("#TinyCurrentFolder").val(), page);
});

function TinyNormalizePastedWordTables(content) {
    if (!content || !/<table/i.test(content)) {
        return content;
    }

    var wrapper = document.createElement("div");
    var styleBlocks = [];
    var htmlContent = content;

    if (/<html[\s>]/i.test(content) || /<body[\s>]/i.test(content)) {
        var parser = new DOMParser();
        var doc = parser.parseFromString(content, "text/html");
        styleBlocks = Array.prototype.map.call(doc.querySelectorAll("style"), function (style) {
            return style.textContent || "";
        });
        htmlContent = doc.body ? doc.body.innerHTML : content;
    } else {
        styleBlocks = (content.match(/<style[\s\S]*?<\/style>/gi) || []).map(function (styleHtml) {
            return styleHtml.replace(/<\/?style[^>]*>/gi, "");
        });
    }

    wrapper.innerHTML = htmlContent
        .replace(/<!--\[if[\s\S]*?<!\[endif\]-->/gi, "")
        .replace(/<style[\s\S]*?<\/style>/gi, "")
        .replace(/<o:p>\s*<\/o:p>/gi, "")
        .replace(/<o:p>[\s\S]*?<\/o:p>/gi, "");

    styleBlocks.forEach(function (styleText) {
        var ruleMatch;
        var ruleRegex = /([^{}]+)\{([^{}]+)\}/g;

        while ((ruleMatch = ruleRegex.exec(styleText)) !== null) {
            var selectors = ruleMatch[1].split(",");
            var declarations = ruleMatch[2].replace(/\s+/g, " ").trim();

            if (!declarations || /^@/i.test(ruleMatch[1].trim())) {
                continue;
            }

            selectors.forEach(function (selector) {
                var match = selector.trim().match(/^(?:(\w+)\s*)?\.([\w-]+)$/);

                if (!match) {
                    return;
                }

                var tagName = match[1] || "*";
                var className = match[2];
                $(wrapper)
                    .find(tagName)
                    .filter(function () {
                        return $(this).hasClass(className);
                    })
                    .each(function () {
                        var currentStyle = this.getAttribute("style") || "";
                        this.setAttribute("style", (currentStyle ? currentStyle + "; " : "") + declarations);
                    });
            });
        }
    });

    $(wrapper)
        .find("table")
        .each(function () {
            var $table = $(this);
            var tableStyle = this.getAttribute("style") || "";
            $table.addClass("table table-bordered");

            if (!/border-collapse\s*:/i.test(tableStyle)) {
                $table.css("border-collapse", "collapse");
            }

            if (!$table.attr("width") && !/width\s*:/i.test(tableStyle)) {
                $table.css("width", "100%");
            }
        });

    $(wrapper)
        .find("th,td")
        .each(function () {
            var $cell = $(this);
            var cellStyle = this.getAttribute("style") || "";

            if (!/border(?:-\w+)?\s*:/i.test(cellStyle)) {
                $cell.css("border", "1px solid #dee2e6");
            }

            if (!/padding(?:-\w+)?\s*:/i.test(cellStyle)) {
                $cell.css("padding", "6px");
            }
        });

    return wrapper.innerHTML;
}

function TinyGetClipboardHtml(event) {
    var nativeEvent = event.raw || event.originalEvent || event;
    var clipboardData =
        event.clipboardData ||
        (nativeEvent && nativeEvent.clipboardData) ||
        window.clipboardData;

    if (!clipboardData || !clipboardData.getData) {
        return "";
    }

    return clipboardData.getData("text/html") || clipboardData.getData("Text") || "";
}

function TinySetupPasteFromWord(editor) {
    editor.on("paste", function (event) {
        var html = TinyGetClipboardHtml(event);
        if (!html || !/<table/i.test(html)) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        if (event.raw && event.raw.stopImmediatePropagation) {
            event.raw.stopImmediatePropagation();
        }

        editor.undoManager.transact(function () {
            editor.insertContent(TinyNormalizePastedWordTables(html));
        });
    });
}

var TinyPasteFromWordOptions = {
    paste_as_text: false,
    paste_data_images: true,
    paste_merge_formats: true,
    paste_enable_default_filters: false,
    paste_retain_style_properties: "all",
    paste_webkit_styles: "all",
    paste_remove_styles_if_webkit: false,
    powerpaste_word_import: "merge",
    powerpaste_html_import: "merge",
    powerpaste_allow_local_images: true,
    powerpaste_keep_unsupported_src: true,
    paste_preprocess: function (_plugin, args) {
        args.content = TinyNormalizePastedWordTables(args.content);
    },
    paste_postprocess: function (_plugin, args) {
        args.node.innerHTML = TinyNormalizePastedWordTables(args.node.innerHTML);
    },
    valid_styles: {
        "*":
            "font-size,font-family,font-weight,font-style,color,text-align,text-decoration,vertical-align,background,background-color,border,border-left,border-right,border-top,border-bottom,border-color,border-style,border-width,border-collapse,width,height,padding,padding-left,padding-right,padding-top,padding-bottom,margin,margin-left,margin-right,margin-top,margin-bottom",
    },
    extended_valid_elements:
        "table[class|style|border|bordercolor|cellpadding|cellspacing|width|height|align],thead[class|style],tbody[class|style],tfoot[class|style],tr[class|style|height|align|valign],td[colspan|rowspan|class|style|width|height|align|valign],th[colspan|rowspan|class|style|width|height|align|valign],colgroup[class|style|width],col[class|style|span|width],span[class|style],p[class|style|align],div[class|style|align]",
};

    tinymce.init({
        selector: 'textarea#full-textarea',
        language: 'vi',
		height: 500,
		content_style: 'body { font-size: 14px; font-family: Arial; }',
		preview_styles: 'font-size color',
		image_uploadtab: false,
        relative_urls: false,
        remove_script_host: false,
		automatic_uploads: false,
        convert_urls: false,
		block_formats: 'Paragraph=p; Header 1=h1; Header 2=h2; Header 3=h3',
        plugins: 'print preview paste importcss searchreplace case autolink autosave save directionality code visualblocks visualchars fullscreen image link media template codesample table charmap hr pagebreak nonbreaking anchor toc insertdatetime advlist lists wordcount imagetools textpattern noneditable help charmap emoticons',
		imagetools_cors_hosts: ['picsum.photos'],
        menubar: 'file edit view insert format tools table help casechange',
        toolbar1: 'undo redo| bold italic underline strikethrough case | blocks fontfamily fontsize| alignleft aligncenter alignright alignjustify | outdent indent |  numlist bullist | forecolor backcolor removeformat | pagebreak | charmap emoticons | fullscreen  preview save print',
		toolbar2: 'insertfile image media template link anchor codesample | ltr rtl | paste pastetext | table tabledelete | tableprops tablerowprops tablecellprops | tableinsertrowbefore tableinsertrowafter tabledeleterow | tableinsertcolbefore tableinsertcolafter tabledeletecol',
        toolbar_sticky: true, 
        paste_as_text: TinyPasteFromWordOptions.paste_as_text,
        paste_data_images: TinyPasteFromWordOptions.paste_data_images,
        paste_merge_formats: TinyPasteFromWordOptions.paste_merge_formats,
        paste_enable_default_filters: TinyPasteFromWordOptions.paste_enable_default_filters,
        paste_retain_style_properties: TinyPasteFromWordOptions.paste_retain_style_properties,
        paste_webkit_styles: TinyPasteFromWordOptions.paste_webkit_styles,
        paste_remove_styles_if_webkit: TinyPasteFromWordOptions.paste_remove_styles_if_webkit,
        powerpaste_word_import: TinyPasteFromWordOptions.powerpaste_word_import,
        powerpaste_html_import: TinyPasteFromWordOptions.powerpaste_html_import,
        powerpaste_allow_local_images: TinyPasteFromWordOptions.powerpaste_allow_local_images,
        powerpaste_keep_unsupported_src: TinyPasteFromWordOptions.powerpaste_keep_unsupported_src,
        paste_preprocess: TinyPasteFromWordOptions.paste_preprocess,
        paste_postprocess: TinyPasteFromWordOptions.paste_postprocess,
        valid_styles: TinyPasteFromWordOptions.valid_styles,
        setup: TinySetupPasteFromWord,
        templates: [
            { title: 'Slideshow', description: 'Tạo bảng cho slideshow', content: '<div class="mceTmpl"><table width="98%%"  border="0" cellspacing="0" cellpadding="0"><tr><th scope="col"> </th><th scope="col"> </th></tr><tr><td> </td><td> </td></tr></table></div>' },
           
        ],        
        extended_valid_elements: TinyPasteFromWordOptions.extended_valid_elements,
        font_size_formats: "6px 8px 9px 10px 11px 12px 13px 14px 15px 16px 17px 18px 19px 20px 21px 22px 23px 24px 30px 36px 48px 60px 72px 96px",
        file_picker_callback: function (callback, value, meta) {
            console.log("file_picker_callback");
            TinyGetListItems();
            $('#tinymce_insert').modal('show');
            $('.btn-insert-file').hide();
            
            $('.tiny-insert-file').show();
            $('#TinyMangerFileType').val("1");

            var path = "";

            jQuery(".tiny-insert-file").on("click", function () {
                 
                path = $("#TinySelectPath").val();
                console.log("tiny-insert-file  click path", path);
                
                if (meta.filetype == 'file') {
					console.log("meta.filetype =file");
                    callback(path, { text: $("#TinySelectNameFile").val() });
                } else if (meta.filetype == 'image') {
					console.log("meta.filetype =image");
                    callback(path, { alt: $("#TinySelectNameFile").val(), width: "100%", height:"100%" });
                } else if (meta.filetype == 'media') {
					console.log("meta.filetype =media");
                    callback(path, { /*source2: 'alt.ogg', poster: 'image.jpg'*/ });
                } else {
                    callback(path, { /*source2: 'alt.ogg', poster: 'image.jpg'*/ });
                }

                $('#tinymce_insert').modal('hide');
            });
			
			jQuery(".dir_file_item").on("dblclick", function () {
                 
                path = $("#TinySelectPath").val();
                console.log("tiny-insert-file  dblclick path", path);
                
            });

        },
        file_picker_types: 'file image media',
        images_upload_handler: function (blobInfo, success, failure) {
            var xhr, formData;

            xhr = new XMLHttpRequest();
            xhr.withCredentials = false;
            xhr.open('POST', '/Home/UploadImage');
            xhr.onload = function () {
                var json;

                if (xhr.status != 200) {
                    failure('HTTP Error: ' + xhr.status);
                    return;
                }

                json = JSON.parse(xhr.responseText);

                if (!json || typeof json.file_path != 'string') {
                    failure('Invalid JSON: ' + xhr.responseText);
                    return;
                }
                success(json.file_path);
            };
            formData = new FormData();
            formData.append('file', blobInfo.blob(), blobInfo.filename());
            xhr.send(formData);
        },
    });
	
tinymce.init({
        selector: 'textarea#full-textarea-en',
        language: 'vi',
		height: 500,
		content_style: 'body { font-size: 14px; font-family: Arial; }',
		preview_styles: 'font-size color',
		image_uploadtab: false,
        relative_urls: false,
        remove_script_host: false,
		automatic_uploads: false,
        convert_urls: false,
		block_formats: 'Paragraph=p; Header 1=h1; Header 2=h2; Header 3=h3',
        plugins: 'print preview paste importcss searchreplace case autolink autosave save directionality code visualblocks visualchars fullscreen image link media template codesample table charmap hr pagebreak nonbreaking anchor toc insertdatetime advlist lists wordcount imagetools textpattern noneditable help charmap emoticons',
		imagetools_cors_hosts: ['picsum.photos'],
        menubar: 'file edit view insert format tools table help casechange',
        toolbar1: 'undo redo| bold italic underline strikethrough case | blocks fontfamily fontsize| alignleft aligncenter alignright alignjustify | outdent indent |  numlist bullist | forecolor backcolor removeformat | pagebreak | charmap emoticons | fullscreen  preview save print',
		toolbar2: 'insertfile image media template link anchor codesample | ltr rtl | paste pastetext | table tabledelete | tableprops tablerowprops tablecellprops | tableinsertrowbefore tableinsertrowafter tabledeleterow | tableinsertcolbefore tableinsertcolafter tabledeletecol',
        toolbar_sticky: true, 
        paste_as_text: TinyPasteFromWordOptions.paste_as_text,
        paste_data_images: TinyPasteFromWordOptions.paste_data_images,
        paste_merge_formats: TinyPasteFromWordOptions.paste_merge_formats,
        paste_enable_default_filters: TinyPasteFromWordOptions.paste_enable_default_filters,
        paste_retain_style_properties: TinyPasteFromWordOptions.paste_retain_style_properties,
        paste_webkit_styles: TinyPasteFromWordOptions.paste_webkit_styles,
        paste_remove_styles_if_webkit: TinyPasteFromWordOptions.paste_remove_styles_if_webkit,
        powerpaste_word_import: TinyPasteFromWordOptions.powerpaste_word_import,
        powerpaste_html_import: TinyPasteFromWordOptions.powerpaste_html_import,
        powerpaste_allow_local_images: TinyPasteFromWordOptions.powerpaste_allow_local_images,
        powerpaste_keep_unsupported_src: TinyPasteFromWordOptions.powerpaste_keep_unsupported_src,
        paste_preprocess: TinyPasteFromWordOptions.paste_preprocess,
        paste_postprocess: TinyPasteFromWordOptions.paste_postprocess,
        valid_styles: TinyPasteFromWordOptions.valid_styles,
        setup: TinySetupPasteFromWord,
        templates: [
            { title: 'Slideshow', description: 'Tạo bảng cho slideshow', content: '<div class="mceTmpl"><table width="98%%"  border="0" cellspacing="0" cellpadding="0"><tr><th scope="col"> </th><th scope="col"> </th></tr><tr><td> </td><td> </td></tr></table></div>' },
           
        ],        
        extended_valid_elements: TinyPasteFromWordOptions.extended_valid_elements,
        font_size_formats: "6px 8px 9px 10px 11px 12px 13px 14px 15px 16px 17px 18px 19px 20px 21px 22px 23px 24px 30px 36px 48px 60px 72px 96px",
        file_picker_callback: function (callback, value, meta) {
            console.log("file_picker_callback");
            TinyGetListItems();
            $('#tinymce_insert').modal('show');
            $('.btn-insert-file').hide();
            
            $('.tiny-insert-file').show();
            $('#TinyMangerFileType').val("1");

            var path = "";

            jQuery(".tiny-insert-file").on("click", function () {
                 
                path = $("#TinySelectPath").val();
                console.log("tiny-insert-file  click path", path);
                
                if (meta.filetype == 'file') {
					console.log("meta.filetype =file");
                    callback(path, { text: $("#TinySelectNameFile").val() });
                } else if (meta.filetype == 'image') {
					console.log("meta.filetype =image");
                    callback(path, { alt: $("#TinySelectNameFile").val(), width: "100%", height:"100%" });
                } else if (meta.filetype == 'media') {
					console.log("meta.filetype =media");
                    callback(path, { /*source2: 'alt.ogg', poster: 'image.jpg'*/ });
                } else {
                    callback(path, { /*source2: 'alt.ogg', poster: 'image.jpg'*/ });
                }

                $('#tinymce_insert').modal('hide');
            });
			
			jQuery(".dir_file_item").on("dblclick", function () {
                 
                path = $("#TinySelectPath").val();
                console.log("tiny-insert-file  dblclick path", path);
                
            });

        },
        file_picker_types: 'file image media',
        images_upload_handler: function (blobInfo, success, failure) {
            var xhr, formData;

            xhr = new XMLHttpRequest();
            xhr.withCredentials = false;
            xhr.open('POST', '/Home/UploadImage');
            xhr.onload = function () {
                var json;

                if (xhr.status != 200) {
                    failure('HTTP Error: ' + xhr.status);
                    return;
                }

                json = JSON.parse(xhr.responseText);

                if (!json || typeof json.file_path != 'string') {
                    failure('Invalid JSON: ' + xhr.responseText);
                    return;
                }
                success(json.file_path);
            };
            formData = new FormData();
            formData.append('file', blobInfo.blob(), blobInfo.filename());
            xhr.send(formData);
        },
    });


function TinyGetListItems(CurrentFolder = '/', page = 1) {
    $("#TinySelectPath").val("");
    $("#tiny-alert").hide();
    $("#TinyCurrentFolder").val(CurrentFolder);

    $("#tinymce_content").html('<div  class="text-center"><div class="lds-dual-ring"></div></div>')
    $("#tiny-pagination").html("");
    var form = { Command: '', Lang: '', Type: '', CurrentFolder: CurrentFolder, Hash:'', CurrentPage: page, ItemsPerPage: TinyItemsPerPage };
    var token = jQuery('input[name="__RequestVerificationToken"]').val();
    var headers = {};
    headers["RequestVerificationToken"] = token;
    
    $.ajax({
        type: "POST",
        url: '/Admin/ManagerFilePrivate/GetListItems',
        headers: headers,
        data: JSON.stringify(form),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (data) {            
            
            htmlBreadcrumb = '<li><a onclick="TinyGetListItems(\'/\')"><i class="fas fa-home"></i></a></li>';
            htmlBreadcrumb = htmlBreadcrumb + '<li><span class="divider">/</span></li>';

            if (data.Success) {
                $("#TinyCurrentFolder").val(data.Data.TinyCurrentFolder);
                TinyDataListItem = data.Data;
                TinyBuildHtmlList(TinyDataListItem);
                TinyRenderPagination(data.Data.Pagination);
                if (data.Data.ListBreadcrumb != null && data.Data.ListBreadcrumb.length > 0) {

                    for (let i = 0; i < data.Data.ListBreadcrumb.length; i++) {
                        
                        if (data.Data.ListBreadcrumb[i].IsActive) {
                            htmlBreadcrumb = htmlBreadcrumb + '<li class="active">' + data.Data.ListBreadcrumb[i].Name+'</li>';
                            htmlBreadcrumb = htmlBreadcrumb + '<li><span class="divider">/</span></li>';
                        } else {
                            if (i == (data.Data.ListBreadcrumb.length-1)) {
                                htmlBreadcrumb = htmlBreadcrumb + '<li >' + data.Data.ListBreadcrumb[i].Name + '</li>';                                
                            } else {
                                htmlBreadcrumb = htmlBreadcrumb + '<li ><a onclick="TinyGetListItems(\'' + data.Data.ListBreadcrumb[i].Path+'\')">' + data.Data.ListBreadcrumb[i].Name + '</a></li>';
                                htmlBreadcrumb = htmlBreadcrumb + '<li><span class="divider">/</span></li>';
                            }
                        }
                    }
                }
                
                $("#TinyBreadcrumb").html(htmlBreadcrumb);
                
               
            }
        },
        error: function () {
            $('.loadding-title').html("Lỗi khi tải file ...");
            $("#tiny-pagination").html("");
            
        }
    });

   
}


function TinyGetItem(CurrentFolder = '/', n = 0) {
    
	
	$(".dir").removeClass("acctive");
	$(".dir_" + n.toString()).addClass("acctive");
	$("#TinySelectPath").val(CurrentFolder);
	var extension = $(".dir_" + n.toString()).data('extension'); //getter            
	$("#TinySelectNameFile").val($(".dir_" + n.toString() + " .box h4 a").text().replace("." + extension, ""));
	
	if( $('#TinyMangerFileType').val()=="2"){	
		ManagerFileSelect('TinyGetItem');
	}else{
		
	}
	
	
}

function TinyGetItemDBClick(CurrentFolder = '/', n = 0) {
	console.log("TinyGetItemDBClick");
   
	
	$(".dir").removeClass("acctive");
	$(".dir_" + n.toString()).addClass("acctive");
	$("#TinySelectPath").val(CurrentFolder);
	var extension = $(".dir_" + n.toString()).data('extension'); //getter            
	$("#TinySelectNameFile").val($(".dir_" + n.toString() + " .box h4 a").text().replace("." + extension, ""));
	
	if( $('#TinyMangerFileType').val()=="2"){	
		ManagerFileSelect('TinyGetItem');
	}else{
		$('.tiny-insert-file').click();
	}
}

function TinyGetMultiItems(CurrentFolder = '/', n = 0) {
    $(".dir").removeClass("acctive");
    $(".dir_" + n.toString()).addClass("acctive");
    $("#TinySelectPath").val(CurrentFolder);
    var extension = $(".dir_" + n.toString()).data('extension'); //getter            
    $("#TinySelectNameFile").val($(".dir_" + n.toString() + " .box h4 a").text().replace("." + extension, ""));
    $("#TinySelectMultiNameFile").val($(".dir_" + n.toString() + " .box h4 a").text().replace("." + extension, ""));
}


function ShowFolderPlus() {
    $("#FormCreateFolder")[0].reset();
    $("#FormCreateFolder .invalid-feedback").hide();    
    $('#FolderPlus').modal('show');
}




function ShowUploadFile() {
    $("#TinyBodyUploadFile").html("<tr><td colspan='5' class='text-center'>Không có file Upload</td></tr>");
    $('#UploadFile').modal('show');
}

function TinyCreateFolder() {
    var FolderName = $("#TinyFolderName").val();
    var CurrentFolder = $("#TinyCurrentFolder").val();
    $("#FormCreateFolder .invalid-feedback").hide();

    if (FolderName == null || FolderName.trim() == '') {
        $("#TinyFolderName").removeClass("is-valid");
        $("#TinyFolderName").addClass("is-invalid");
        $("#FormCreateFolder .invalid-feedback").show();
    } else {
        var form = { Path: CurrentFolder, Name: FolderName };
        var token = jQuery('input[name="__RequestVerificationToken"]').val();
        var headers = {};
        headers["RequestVerificationToken"] = token;

        $.ajax({
            type: "POST",
            url: '/Admin/ManagerFilePrivate/CreateFolder',
            headers: headers,
            data: JSON.stringify(form),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {
                $("#TinyFolderName").val("");
                if (data.Success) {
                    $('#FolderPlus').modal('hide');
                    TinyGetListItems(CurrentFolder);
                } else {
                    $("#TinyFolderName").focus();
                    $("#TinyFolderName").removeClass("is-valid");
                    $("#TinyFolderName").addClass("is-invalid");
                    $("#FormCreateFolder .invalid-feedback").html(data.Msg);
                    $("#FormCreateFolder .invalid-feedback").show();
                }

            },
            error: function () {
                $('.loadding-title').html("Lỗi khi tải file ...");

            }
        });
    }

    
}

$("#TinyFolderName").keyup(function () {
    

    $("#TinyFolderName").val(string_to_slug($("#TinyFolderName").val()));
    var FolderName = $("#TinyFolderName").val();

    if (FolderName == null || FolderName.trim() == '') {
        $("#TinyFolderName").removeClass("is-valid");
        $("#TinyFolderName").addClass("is-invalid");
        $("#FormCreateFolder .invalid-feedback").show();
    } else {
        $("#TinyFolderName").removeClass("is-invalid");
        $("#TinyFolderName").addClass("is-valid");   
        $("#FormCreateFolder .invalid-feedback").hide();
    }
});



function TinyUploadFiles() {
    $("#TinyBodyUploadFile").html('<tr><td colspan="5" class="text-center">Đang tải file lên. Xin vui lòng đợi ...</td></tr>');
    var formData = new FormData();
    for (var i = 0; i < $('#TinyUploadFile')[0].files.length; ++i) {
        formData.append('file', $('#TinyUploadFile')[0].files[i]);
    }    
    var CurrentFolder = $("#TinyCurrentFolder").val();
    formData.append("CurrentFolder", CurrentFolder );

    $("#FormCreateFolder .invalid-feedback").hide();

    var token = jQuery('input[name="__RequestVerificationToken"]').val();
    var headers = {};
    headers["RequestVerificationToken"] = token;


    $.ajax({
        type: "POST",
        url: '/Admin/ManagerFilePrivate/UploadFiles',
        headers: headers,        
        data: formData,
        processData: false,  // tell jQuery not to process the data
        contentType: false,  // tell jQuery not to set contentType        
        success: function (data) {
            var html = "";
            var ListItems = [];
            if (data.Success) {
                TinyGetListItems(CurrentFolder);
                if (data.Data != null && data.Data.length > 0) {
                    ListItems = data.Data;
                    for (let i = 0; i < data.Data.length; i++)
                    {
                        html = html + "<tr>";
                        html = html + "<td scope='row'>" + (i + 1).toString() + "</td>";
                        if (ListItems[i].Selected) {
                            html = html + "<td scope='row'>" + ListItems[i].Name + "</td>";
                        } else {
                            html = html + "<td scope='row'>" + ListItems[i].Name + "<small class='text-danger'> (" + ListItems[i].Msg + ")</small></td>";
                        }  
                        
                        html = html + "<td scope='row'>" + ListItems[i].StrSize + "</td>";
                        if (ListItems[i].Selected) {
                            html = html + "<td scope='row' class='text-center'><i class='fas fa-check text-primary'></i></td>";
                        } else {
                            html = html + "<td scope='row' class='text-center'><i class='fas fa-times text-danger'></i></td>";
                        }                        
                        html = html + "</tr>";
                    }
                }
            } else {
                
            }
            $("#TinyBodyUploadFile").html(html);
        },
        error: function () {
            $('.loadding-title').html("Lỗi khi tải file ...");

        }
    });

}

$('#TinyUploadFile').change(function () {
    var names = [];
    var html = "";
    for (var i = 0; i < $(this).get(0).files.length; ++i) {
        html = html + "<tr>";
        html = html + "<td scope='row'>" + (i + 1).toString() + "</td>";        
        html = html + "<td scope='row'>" + $(this).get(0).files[i].name + "</td>";
        html = html + "<td scope='row'>" + $(this).get(0).files[i].size + "</td>";
        html = html + "<td scope='row' class='text-center'><i class='fas fa-check text-primary'></i></td>";
        html = html + "</tr>";
        console.log($(this).get(0).files[i]);
        names.push($(this).get(0).files[i].name);
        console.log(names[i]);
    }
    $("#TinyBodyUploadFile").html(html);
});

function TinyDelete(path,name,n,i)
{
    var urlPath = '/Admin/ManagerFilePrivate/DeleteFolder';
    var Msg = "Đang xóa folder <strong>" + name + "</strong> Xin vui lòng đơi";    
    if (i == 2) { urlPath = '/Admin/ManagerFilePrivate/DeleteFile';  Msg = "Đang xóa file <strong>" + name + "</strong> Xin vui lòng đơi";}           
    $("#tiny-alert").show();
    $("#tiny-alert .tiny-alert-msg").html(Msg);
    $("#tiny-alert .alert").removeClass("alert-danger");
    $("#tiny-alert .alert").addClass("alert-success");

    var token = jQuery('input[name="__RequestVerificationToken"]').val();
    var headers = {};
    headers["RequestVerificationToken"] = token;
    var form = { Path: path, Name: name };

    $.ajax({
        type: "POST",
        url: urlPath,
        headers: headers,
        data: JSON.stringify(form), 
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (data) {
            
            $("#tinymce_body").animate({ scrollTop: 0 }, 600);
            $("#tiny-alert").show();

            $("#tiny-alert .tiny-alert-msg").html(data.Msg);
            if (data.Success == false) {
                $("#tiny-alert .alert").removeClass("alert-success");
                $("#tiny-alert .alert").addClass("alert-danger");
            } else {
                if (i == 1) {
                    $(".dir_folder_" + n.toString()).hide();
                } else {
                    $(".dir_" + n.toString()).hide();
                }
            }            
        },
        error: function () {
            Msg = "Bạn không có quyền. Xin vui lòng liên hệ với ban quản trị";   
            $("#tiny-alert .tiny-alert-msg").html(Msg);
            $("#tiny-alert .alert").removeClass("alert-success");
            $("#tiny-alert .alert").addClass("alert-danger");
        }
    });
}

function TinyShowRename(n, i) {
    $("#FormRename .invalid-feedback").hide();
    $("#FormRename")[0].reset();
    var name = "";
    var path = "";
    $("#Rename_N").val(n);
    $("#Rename_I").val(i);

    if (i == 1) {
        $("#FormRename .input-group-prepend").hide();
        name = $(".dir_folder_" + n.toString() + " .box h4 a").text();
        path = $("#TinyCurrentFolder").val() + name;
    } else if (i == 2) {
        var extension = $(".dir_" + n.toString()).data('extension'); //getter        
        name = $(".dir_" + n.toString() + " .box h4 a").text().replace("." + extension, "");      
        path = $("#TinyCurrentFolder").val() + name + "." + extension;
        $("#TinyRenameExtension").html("."+extension);
    }

    $("#Rename_Path").val(path);
    $('#TinyShowRename').modal('show');
    $("#TinyRenameName").val(name);
    
    

}

function TinyRename() {
    var token = jQuery('input[name="__RequestVerificationToken"]').val();
    var headers = {};
    headers["RequestVerificationToken"] = token;
    var name = $("#TinyRenameName").val();
    var path = $("#Rename_Path").val();
    var N =  $("#Rename_N").val();
    var I = $("#Rename_I").val();
    var name_old = "";
    if (I == 2) {
        var extension = $(".dir_" + N.toString()).data('extension'); //getter
        name = name + "." + extension;
        $("#FormRename .input-group-prepend").show();
        name_old = $(".dir_" + N.toString() + " .box h4 a").text().replace("." + extension, "");
    } else {
        name_old = name = $(".dir_folder_" + N.toString() + " .box h4 a").text();
    }
    if (name == name_old) {
        $("#FormRename .invalid-feedback").html("Tên Mới trùng với tên Cũ");
        $("#FormRename .invalid-feedback").show();
    } else {
        var form = { Path: path, Name: name, flag: I };

        $.ajax({
            type: "POST",
            url: '/Admin/ManagerFilePrivate/Rename',
            headers: headers,
            data: JSON.stringify(form),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: function (data) {

                if (data.Success == false) {
                    $("#FormRename .invalid-feedback").html(data.Msg);
                    $("#FormRename .invalid-feedback").show();

                } else {
                    $('#TinyShowRename').modal('hide');
                    $("#FormRename")[0].reset();
                    if (I == 1) {
                        $(".dir_folder_" + N.toString() + " .box h4 a").text(name);
                    } else {
                        $(".dir_" + N.toString() + " .box h4 a").text(name);
                    }
                }
            },
            error: function () {
                Msg = "Bạn không có quyền. Xin vui lòng liên hệ với ban quản trị";
                $("#tiny-alert .tiny-alert-msg").html(Msg);
                $("#tiny-alert .alert").removeClass("alert-success");
                $("#tiny-alert .alert").addClass("alert-danger");
            }
        });
    }
    
}

function TinyAlertClose() {
    $("#tiny-alert").hide();
}

function TinyResetItems() {
    TinyGetListItems($("#TinyCurrentFolder").val());
}

function TinyHelper() {
    console.log(TinyDataListItem);
}

$("#tiny_filter_keyword").on("keyup", function () {
    TinySearchListItems();
});

function TinySearchListItems() {
    var keyword = $("#tiny_filter_keyword").val().toLowerCase();    
    if (TinyDataListItem.ListFolders != null && TinyDataListItem.ListFolders.length > 0) {
        for (let i = 0; i < TinyDataListItem.ListFolders.length; i++) {
            if (TinyDataListItem.ListFolders[i].Name.toLowerCase().indexOf(keyword) > -1) {
                $(".dir_folder_" + i.toString()).show();
            } else {
                $(".dir_folder_" + i.toString()).hide();
            }
        }
    }

    if (TinyDataListItem.ListFiles != null && TinyDataListItem.ListFiles.length > 0) {
        for (let i = 0; i < TinyDataListItem.ListFiles.length; i++) {
            if (TinyDataListItem.ListFiles[i].Name.toLowerCase().indexOf(keyword) > -1) {
                $(".dir_" + i.toString()).show();
            } else {
                $(".dir_" + i.toString()).hide();
            }
        }
    }
}
function TinyBuildHtmlList(Data) {
    var html = "";
    if (Data.ListFolders != null && Data.ListFolders.length > 0) {
        for (let i = 0; i < Data.ListFolders.length; i++) {
            html = html + "<li class='dir dir_folder_" + i.toString() + "'>";
            html = html + "<div class='tiny-directory' onclick='TinyGetListItems(\"" + Data.ListFolders[i].Path + "\")' data-name='" + Data.ListFolders[i].Name + "' data-path='" + Data.ListFolders[i].Path + "' data-pathparent='" + Data.ListFolders[i].PathParent + "'>";
            html = html + "<a  class='folder-link'>";
            html = html + "<img style='width:122px;height:91px;' src='" + Data.ListFolders[i].Img + "' class='directory-img loaded' data-src='" + Data.ListFolders[i].Img + "' />";
            html = html + "</a>";
            html = html + "</div>";
            html = html + "<div class='box' onclick='TinyGetListItems(\"" + Data.ListFolders[i].Path + "\")'>";
            html = html + "<h4><a >" + Data.ListFolders[i].Name + "</a></h4>";
            html = html + "</div>";
            html = html + "<div class='tiny-action'>";
            html = html + "<i class='fas fa-pencil-alt' onclick='TinyShowRename(" + i + ",1)'></i>";
            html = html + "<i class='fas fa-trash-alt' onclick='TinyDelete(\"" + Data.ListFolders[i].Path + "\",\"" + Data.ListFolders[i].Name + "\"," + i + ",1)'></i>";
            html = html + "</div>";
            html = html + "</li>";
        }



    }

    if (Data.ListFiles != null && Data.ListFiles.length > 0) {
        for (let i = 0; i < Data.ListFiles.length; i++) {
            html = html + "<li class='dir dir_file dir_" + i.toString() + "' data-pathparent='" + Data.ListFiles[i].PathParent + "' data-extension='" + Data.ListFiles[i].Extension + "'>";
            html = html + "<div class='tiny-directory dir_file_item' onclick='TinyGetItem(\"" + Data.ListFiles[i].Path + "\"," + i + ")' data-name='" + Data.ListFiles[i].Name + "' data-path='" + Data.ListFiles[i].Path + "' ondblclick='TinyGetItemDBClick(\"" + Data.ListFiles[i].Path + "\"," + i + ")' data-name='" + Data.ListFiles[i].Name + "' data-path='" + Data.ListFiles[i].Path + "' >";
            html = html + "<a  class='folder-link'>";
            html = html + "<img style='width:122px;height:91px;' src='" + Data.ListFiles[i].Img + "' class='directory-img loaded' data-src='" + Data.ListFiles[i].Img + "' />";
            html = html + "</a>";
            html = html + "</div>";
            html = html + "<div class='box ' onclick='TinyGetItem(\"" + Data.ListFiles[i].Path + "\"," + i + ")' ondblclick='TinyGetItemDBClick(\"" + Data.ListFiles[i].Path + "\"," + i + ")' data-path='" + Data.ListFiles[i].Path + "'>";
            html = html + "<h4><a >" + Data.ListFiles[i].Name + "</a></h4>";
            html = html + "</div>";
            html = html + "<div class='tiny-action'>";
            html = html + "<i class='fas fa-pencil-alt' onclick='TinyShowRename(" + i + ",2)'></i>";
            html = html + "<i class='fas fa-trash-alt' onclick='TinyDelete(\"" + Data.ListFiles[i].Path + "\",\"" + Data.ListFiles[i].Name + "\"," + i + ",2)'></i>";
            html = html + "</div>";
			if($('#TinyMangerFileType').val()=="2"){
				html = html + "<div class='tiny-checkbox'>";
				html = html + `<input type="checkbox" class="tam-checkbox__input" id="checkbox_muiti_file_` + i + `" name="checkbox_muiti_file_` + i + `" value="` + Data.ListFiles[i].Path +`">`;
				html = html + "</div>";
			}
            html = html + "</li>";
        }
    }    
    $("#tinymce_content").html(html);
}


function ShowManagerFile(id='',type='')
{
	console.log("type:"+type+";Id="+id);
    $(".btn-insert-file").hide();
    
    $("#TinyOutputId").val(id);
    $('#TinyMangerFileType').val("2");
    TinyGetListItems();
    $('#tinymce_insert').modal('show');
    if (type == 'all') {
        $(".ouput-insert-file").show();
    }
}
/*
function ManagerFileSelect()
{
    console.log("ManagerFileSelect");
    $('#tinymce_insert').modal('hide');
    var id = $("#TinyOutputId").val();    
    $("#" + id).val($("#TinySelectPath").val());
    $('.' + id + '_Preview').attr("src", $("#TinySelectPath").val());
}
*/

function ManagerFileSelect(action ='TinyGetItem')
{
    console.log("ManagerFileSelect - " + action);
    if (action == 'TinyGetItem') {
        var id = $("#TinyOutputId").val();
        var selectedPath = $("#TinySelectPath").val();
		console.log("Id="+id);
        if (id && id.trim() !== '') {
            $("#" + id).val(selectedPath);
            $('.' + id + '_Preview').attr("src", selectedPath);
        }
        $('#tinymce_insert').modal('hide');
    } else {
        var file_check = [];
        $.each($("#tinymce_insert input[type='checkbox']:checked"), function () {
            file_check.push($(this).val());
        });
        console.log(file_check);
        //var callBackMuitifile = ''
        

        callBackMuitifile(file_check);
    }
   
}

function ViewImageMain(id) {
    //$('#MainShowImagePath').attr("src", "/images/no-image.png");
    $('#MainShowImage').modal('show');
    $('#MainShowImagePath').attr("src", $('#' + id).val());
}

