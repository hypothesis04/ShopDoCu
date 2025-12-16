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