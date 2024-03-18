//
// Created by Joy Zheng on 2024/3/18.
//

#ifndef COMPRESSOR_QTUI_H
#define COMPRESSOR_QTUI_H

#include <QtWidgets>

class DragDropWidget : public QFrame {
public:
    explicit DragDropWidget(QWidget *parent = nullptr) : QFrame(parent) {
        setMinimumSize(200, 200);
        setFrameStyle(QFrame::Sunken | QFrame::StyledPanel);
        setAcceptDrops(true);
    }
protected:
    void dragEnterEvent(QDragEnterEvent *event) override {
        if (event->mimeData()->hasUrls()) {
            event->acceptProposedAction();
        }
    }

    void dragMoveEvent(QDragMoveEvent *event) override {
        if (event->mimeData()->hasUrls()) {
            event->acceptProposedAction();
        }
    }

    void dropEvent(QDropEvent *event) override {
        const QMimeData *mimeData = event->mimeData();
        if (mimeData->hasUrls()) {
            QList<QUrl> urlList = mimeData->urls();
            QString filePath = urlList.at(0).toLocalFile();

            qDebug() << "Dropped file path:" << filePath;
//            event->acceptProposedAction();
        }
    }
};

#endif //COMPRESSOR_QTUI_H
