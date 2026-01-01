// --- PHẦN 1: SCROLL MƯỢT & XỬ LÝ LINK ---
function handleSmoothScroll(event, targetId) {
    const targetUrl = event.currentTarget.getAttribute('href');
    const currentPath = window.location.pathname;

    if (targetUrl !== '#' && !currentPath.endsWith(targetUrl)) {
        localStorage.setItem('scrollToTarget', targetId);
        return true;
    }
    event.preventDefault();
    const targetElement = document.getElementById(targetId);
    if (targetElement) {
        targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        targetElement.querySelector('input[type="text"], input[type="email"], input[type="password"]')?.focus();
    }
}

document.addEventListener('DOMContentLoaded', function() {
    const targetId = localStorage.getItem('scrollToTarget');
    if (targetId) {
        setTimeout(() => {
            const targetElement = document.getElementById(targetId);
            if (targetElement) {
                targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                targetElement.querySelector('input[type="text"], input[type="email"]')?.focus();
            }
            localStorage.removeItem('scrollToTarget');
        }, 100);
    }
});

// --- PHẦN 2: ĐĂNG KÝ BÁN HÀNG (AJAX) ---
$(function () {
    var sellerForm = $('#seller-registration-form');
    if (sellerForm.length) {
        sellerForm.on('submit', function (e) {
            e.preventDefault();
            var form = $(this);

            if (!form.valid()) {
                var firstError = form.find('.is-invalid').first();
                 if (firstError.length) {
                    firstError[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                return;
            }

            $.ajax({
                type: "POST",
                url: form.attr('action'),
                data: form.serialize(),
                success: function (response) {
                    if (response.success) {
                        var successHtml = `
                            <div class="text-center my-5">
                                <i class="bi bi-check-circle-fill text-success" style="font-size: 4rem;"></i>
                                <h4 class="mt-3 text-success">${response.message}</h4>
                                <p class="lead">Yêu cầu đang chờ duyệt.</p>
                                <a href="/" class="btn btn-primary mt-3">Về Trang chủ</a>
                            </div>`;
                        $('#registration-container').html(successHtml);
                    } else {
                        var errorDiv = $('#form-errors');
                        var errorList = '<ul>' + (response.errors ? response.errors.map(e => `<li>${e}</li>`).join('') : `<li>${response.message}</li>`) + '</ul>';
                        errorDiv.html(errorList).show();
                        errorDiv[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                },
                error: function () {
                    var errorDiv = $('#form-errors');
                    errorDiv.html('Lỗi kết nối server. Vui lòng thử lại.').show();
                    errorDiv[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            });
        });
    }
});

// --- PHẦN 3: TẠO & SỬA SẢN PHẨM (Create/Edit Product) ---
$(function () {
    const parentCategory = $('#parentCategory');
    const childCategory = $('#childCategory');
    // ĐÃ SỬA: Đổi productImages thành imageInput để khớp với View
    const imageInput = $('#imageInput'); 
    const imagePreview = $('#imagePreview');
    const mainImageSelect = $('#mainImageSelect');

    // Chỉ chạy nếu đang ở trang có form này
    if (imageInput.length === 0 && parentCategory.length === 0) return;

    // 1. Load danh mục con
    parentCategory.on('change', function () {
        const parentId = $(this).val();
        const url = $(this).data('url') || '/Product/GetChildCategories'; 
        
        childCategory.prop('disabled', true).html('<option value="">-- Đang tải... --</option>');

        if (parentId) {
            $.getJSON(url, { parentId: parentId }, function(data) {
                if (data && data.length > 0) {
                    childCategory.empty().append('<option value="">-- Chọn danh mục con --</option>');
                    $.each(data, function(i, item) {
                        childCategory.append($('<option>').val(item.categoryId).text(item.categoryName));
                    });
                    childCategory.prop('disabled', false);
                } else {
                    childCategory.html('<option value="">-- Không có danh mục con --</option>');
                }
            }).fail(function() {
                childCategory.html('<option value="">-- Lỗi tải dữ liệu --</option>');
            });
        } else {
            childCategory.html('<option value="">-- Chọn danh mục cha trước --</option>');
        }
    });

    // 2. Preview ảnh & Main Image
    imageInput.on('change', function (e) {
        const files = e.target.files;
        
        imagePreview.empty(); 
        mainImageSelect.empty().append('<option value="">-- Chọn ảnh chính --</option>');
        mainImageSelect.prop('disabled', true);

        if (files.length === 0) return;

        if (files.length < 3) {
            imagePreview.html('<div class="col-12"><div class="alert alert-warning py-2 small"><i class="bi bi-exclamation-triangle me-2"></i>Vui lòng chọn tối thiểu 3 ảnh.</div></div>');
        }

        mainImageSelect.prop('disabled', false);

        Array.from(files).forEach((file, index) => {
            const reader = new FileReader();
            reader.onload = function (event) {
                const html = `
                    <div class="col-4 col-md-3 mb-3">
                        <div class="card h-100 border shadow-sm">
                            <img src="${event.target.result}" class="card-img-top" style="height: 100px; object-fit: contain;">
                            <div class="card-footer p-1 text-center small bg-white text-truncate">
                                Ảnh ${index + 1}
                            </div>
                        </div>
                    </div>`;
                imagePreview.append(html);
            };
            reader.readAsDataURL(file);
            mainImageSelect.append(`<option value="${index}">Ảnh ${index + 1}</option>`);
        });

        mainImageSelect.val(0);
    });
});

// --- LOGIC TÌM KIẾM LIVE SEARCH (ADMIN) ---
// Biến này phải nằm ngoài hàm để giữ trạng thái
var delayTimer;

function autoSearch(inputElement) {
    // 1. Xóa lệnh cũ
    clearTimeout(delayTimer);

    // 2. Đợi 500ms
    delayTimer = setTimeout(function() {
        var query = $(inputElement).val();
        var form = $(inputElement).closest('form');
        var url = form.attr('action');

        // 3. Gửi AJAX
        $.ajax({
            url: url,
            type: 'GET',
            data: { searchString: query },
            success: function(response) {
                // 4. Thay thế bảng kết quả
                // Lưu ý: View trả về phải có div id="searchResultTable" bao quanh bảng
                var newTable = $(response).find('#searchResultTable').html();
                $('#searchResultTable').html(newTable);
            },
            error: function() {
                console.log("Lỗi tìm kiếm live");
            }
        });
    }, 500);
}
// --- PHẦN 4: XỬ LÝ FORM ĐĂNG BÁN & SỬA SẢN PHẨM (AJAX) ---
$(function () {
    // Áp dụng cho cả 2 form: Tạo mới và Chỉnh sửa
    var productForm = $('#create-product-form, #edit-product-form');

    if (productForm.length) {
        productForm.on('submit', function (e) {
            e.preventDefault(); // 1. Chặn load lại trang

            // --- QUAN TRỌNG: GOM DỮ LIỆU THÔNG SỐ (SPECS) THÀNH JSON ---
            // Đoạn này thay thế cho code JS rời rạc trong View
            const specs = [];
            $('#specifications-list .spec-row').each(function() {
                const key = $(this).find('input').eq(0).val().trim();
                const value = $(this).find('input').eq(1).val().trim();
                if (key && value) specs.push({ Key: key, Value: value });
            });
            // Gán vào input ẩn để gửi đi
            $('#specifications-json').val(specs.length ? JSON.stringify(specs) : '');

            // --- BẮT ĐẦU GỬI AJAX ---
            var form = $(this);
            var btn = form.find('button[type="submit"]');
            var errorDiv = $('#form-error-msg');
            var containerId = form.attr('id') === 'create-product-form' ? '#create-product-container' : '#edit-product-container';

            // Hiệu ứng loading
            btn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-2"></span>Đang xử lý...');
            errorDiv.addClass('d-none');

            var formData = new FormData(this);

            $.ajax({
                type: "POST",
                url: form.attr('action'),
                data: formData,
                processData: false, 
                contentType: false, 
                success: function (response) {
                    if (response.success) {
                        // --- HIỆN DẤU TÍCH XANH ---
                        var successHtml = `
                            <div class="card border-0 shadow-sm text-center py-5">
                                <div class="card-body">
                                    <div class="mb-4">
                                        <i class="bi bi-check-circle-fill text-success" style="font-size: 5rem; animation: popIn 0.5s cubic-bezier(0.175, 0.885, 0.32, 1.275);"></i>
                                    </div>
                                    <h2 class="fw-bold text-success mb-3">${response.title || "Thành công!"}</h2>
                                    <p class="text-muted lead mb-4">${response.message}</p>
                                    
                                    <div class="d-flex justify-content-center gap-3">
                                        <a href="/SellerChannel/MyProducts" class="btn btn-outline-primary px-4 py-2">
                                            <i class="bi bi-box-seam me-2"></i>Quản lý sản phẩm
                                        </a>
                                        <a href="/SellerChannel/CreateProduct" class="btn btn-primary px-4 py-2">
                                            <i class="bi bi-plus-lg me-2"></i>Đăng sản phẩm mới
                                        </a>
                                    </div>
                                </div>
                            </div>
                            <style>@keyframes popIn { 0% { transform: scale(0); opacity: 0; } 100% { transform: scale(1); opacity: 1; } }</style>
                        `;
                        
                        // Thay thế giao diện form bằng thông báo
                        $(containerId).html(successHtml);
                        document.getElementById(containerId.substring(1)).scrollIntoView({ behavior: 'smooth' });

                    } else {
                        // Lỗi Server trả về
                        errorDiv.html('<i class="bi bi-exclamation-triangle-fill me-2"></i>' + response.message).removeClass('d-none');
                        btn.prop('disabled', false).html('<i class="bi bi-save me-1"></i> Thử lại');
                    }
                },
                error: function () {
                    errorDiv.html('Lỗi kết nối Server. Vui lòng thử lại.').removeClass('d-none');
                    btn.prop('disabled', false).html('<i class="bi bi-save me-1"></i> Thử lại');
                }
            });
        });
    }
});